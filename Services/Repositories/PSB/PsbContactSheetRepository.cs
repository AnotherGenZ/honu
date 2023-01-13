﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using watchtower.Models.PSB;
using watchtower.Services.Repositories.Readers;

namespace watchtower.Services.Repositories.PSB {

    public class PsbContactSheetRepository {

        private readonly ILogger<PsbContactSheetRepository> _Logger;
        private readonly IMemoryCache _Cache;
        private const string CACHE_KEY_PRACTICE_ALL = "Psb.Contacts.Practice";

        private readonly IOptions<PsbDriveSettings> _Settings;

        private readonly ISheetsReader<PsbContact> _Reader;

        private bool _Initialized = false;
        private bool _FailedInit { get { return _FailedInitReason != null; } }
        private string? _FailedInitReason { get; set; } = null;

        private ServiceAccountCredential? _GoogleCredentials = null;
        private DriveService? _DriveService = null;
        private SheetsService? _SheetService = null;

        public PsbContactSheetRepository(ILogger<PsbContactSheetRepository> logger, IMemoryCache cache,
            IOptions<PsbDriveSettings> settings, ISheetsReader<PsbContact> reader) {

            _Logger = logger;
            _Cache = cache;

            _Settings = settings;
            _Reader = reader;
        }

        /// <summary>
        ///     Initialize the repository (if needed). If any error occurs, call <see cref="GetInitializeFailureReason"/> to see why
        /// </summary>
        /// <returns>If the repo was succesfully initialized or not</returns>
        private bool Initialize() {
            if (_Initialized == true) {
                return true;
            }

            if (_FailedInit == true) {
                return false;
            }

            if (_Settings.Value.CredentialFile.Trim().Length == 0) {
                _FailedInitReason = $"credential file from settings is blank";
                _Logger.LogError($"Failed to initialize psb drive repository: {_FailedInitReason}");
                return false;
            }

            if (_Settings.Value.PracticeFolderId.Trim().Length == 0) {
                _FailedInitReason = $"practice folder id from settings is blank";
                _Logger.LogError($"Failed to initialize psb drive repository: {_FailedInitReason}");
                return false;
            }

            if (File.Exists(_Settings.Value.CredentialFile) == false) {
                _FailedInitReason = $"credential file '{_Settings.Value.CredentialFile}' does not exist (or not permission)";
                _Logger.LogError($"Failed to initialize psb drive repository: {_FailedInitReason}");
                return false;
            }

            string[] scopes = {
                DriveService.Scope.Drive,
                DriveService.Scope.DriveFile,
                DriveService.Scope.DriveMetadata,
                DriveService.Scope.DriveAppdata
            };

            try {
                using (FileStream stream = new FileStream(_Settings.Value.CredentialFile, FileMode.Open, FileAccess.Read)) {
                    _GoogleCredentials = ServiceAccountCredential.FromServiceAccountData(stream);

                    _GoogleCredentials = new ServiceAccountCredential(new ServiceAccountCredential.Initializer(_GoogleCredentials.Id) {
                        User = _GoogleCredentials.User,
                        Key = _GoogleCredentials.Key,
                        KeyId = _GoogleCredentials.KeyId,
                        Scopes = scopes
                    });
                }
            } catch (Exception ex) {
                _FailedInitReason = $"exception while opening credential file: {ex.Message}";
                _Logger.LogError(ex, $"Failed to initilaize psb contact sheet repository: {_FailedInitReason}");
                return false;
            }

            if (_GoogleCredentials == null) {
                _FailedInitReason = $"google credentials is still null?";
                _Logger.LogError($"Failed to initialize psb contact sheet repository: {_FailedInitReason}");
                return false;
            }

            BaseClientService.Initializer gClient = new BaseClientService.Initializer() {
                ApplicationName = "Honu/Spark",
                HttpClientInitializer = _GoogleCredentials
            };

            _DriveService = new DriveService(gClient);
            _SheetService = new SheetsService(gClient);

            _Initialized = true;
            return true;
        }

        /// <summary>
        ///     Get the reason for initialization failure, or null if none
        /// </summary>
        public string? GetInitializeFailureReason() {
            return _FailedInitReason;
        }

        /// <summary>
        ///     Clear cached values
        /// </summary>
        public void ClearCache() {
            _Cache.Remove(CACHE_KEY_PRACTICE_ALL);
        }

        /// <summary>
        ///     Get a list of <see cref="PsbContact"/>s from the practice account rep sheet
        /// </summary>
        /// <remarks>
        ///     Responses are cached aboslute from now for 10 minutes. Use <see cref="ClearCache"/>
        ///     if you need it sooner
        /// </remarks>
        public async Task<List<PsbContact>?> GetPracticeContacts() {
            if (_Cache.TryGetValue(CACHE_KEY_PRACTICE_ALL, out List<PsbContact>? contacts) == true) {
                return contacts;
            }

            if (Initialize() == false) {
                return null;
            }

            if (_SheetService == null) {
                throw new SystemException($"Sheet service is not supposed to be null");
            }

            // https://developers.google.com/sheets/api/guides/concepts#cell
            SpreadsheetsResource.ValuesResource.GetRequest sheetR = _SheetService.Spreadsheets.Values.Get(_Settings.Value.ContactSheets.Practice, "Current!A:E");
            Google.Apis.Sheets.v4.Data.ValueRange res = await sheetR.ExecuteAsync();

            if (res.Values.Count < 1) {
                _Logger.LogWarning($"Getting practice rep sheet returned {res.Values} values, expected >0");
                return null;
            }

            IList<object> header = res.Values[0];
            if (header.Count != 5) {
                throw new ArgumentException($"header of practice rep sheet had {header.Count} values, not 5");
            }

            List<string> validationErrors = new();
            if (header[0].ToString() != "Tag") { validationErrors.Add($"Expected column one to be 'Tag', is '{header[0]}'");  }
            if (header[1].ToString() != "In-Game Name") { validationErrors.Add($"Expected column one to be 'In-Game Name', is '{header[1]}'");  }
            if (header[2].ToString() != "E-Mail") { validationErrors.Add($"Expected column one to be 'E-Mail', is '{header[2]}'");  }
            if (header[3].ToString() != "Discord") { validationErrors.Add($"Expected column one to be 'Discord', is '{header[3]}'");  }
            if (header[4].ToString() != "Discord ID") { validationErrors.Add($"Expected column one to be 'Discord ID', is '{header[4]}'");  }

            if (validationErrors.Count > 0) {
                throw new ArgumentException($"Validation errors on practice rep sheet: {string.Join("; ", validationErrors)}");
            }

            // skip header
            contacts = _Reader.ReadList(res.Values.Skip(1));
            _Cache.Set(CACHE_KEY_PRACTICE_ALL, contacts, new MemoryCacheEntryOptions() {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return contacts;
        }

    }
}