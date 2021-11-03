﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using watchtower.Models.Census;
using watchtower.Models.CharacterViewer.WeaponStats;
using watchtower.Services.Census;
using watchtower.Services.Db;
using watchtower.Services.Repositories;

namespace watchtower.Services.Hosted {

    public class HostedBackgroundCharacterWeaponStatQueue : BackgroundService {

        private const string SERVICE_NAME = "background_character_cache";

        private readonly ILogger<HostedBackgroundCharacterWeaponStatQueue> _Logger;
        private readonly IBackgroundCharacterWeaponStatQueue _Queue;

        private readonly ICharacterWeaponStatCollection _WeaponCensus;
        private readonly ICharacterWeaponStatDbStore _WeaponStatDb;
        private readonly ICharacterHistoryStatCollection _HistoryCensus;
        private readonly ICharacterHistoryStatDbStore _HistoryDb;

        private static int _Count = 0;

        public HostedBackgroundCharacterWeaponStatQueue(ILogger<HostedBackgroundCharacterWeaponStatQueue> logger,
            IBackgroundCharacterWeaponStatQueue queue,
            ICharacterWeaponStatDbStore db, ICharacterWeaponStatCollection weaponColl,
            ICharacterHistoryStatDbStore hDb, ICharacterHistoryStatCollection hColl) {

            _Logger = logger;

            _Queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _WeaponStatDb = db ?? throw new ArgumentNullException(nameof(db));
            _WeaponCensus = weaponColl ?? throw new ArgumentNullException(nameof(weaponColl));
            _HistoryCensus = hColl;
            _HistoryDb = hDb;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            int errorCount = 0;

            while (stoppingToken.IsCancellationRequested == false) {
                try {
                    string charID = await _Queue.Dequeue(stoppingToken);

                    List<WeaponStatEntry> entries = await _WeaponCensus.GetByCharacterID(charID);
                    foreach (WeaponStatEntry entry in entries) {
                        await _WeaponStatDb.Upsert(entry);
                    }

                    List<PsCharacterHistoryStat> stats = await _HistoryCensus.GetByCharacterID(charID);
                    foreach (PsCharacterHistoryStat stat in stats) {
                        await _HistoryDb.Upsert(charID, stat.Type, stat);
                    }

                    ++_Count;

                    if (_Count % 100 == 0) {
                        _Logger.LogDebug($"Cached {_Count} characters");
                    }

                    errorCount = 0;
                } catch (Exception ex) when (stoppingToken.IsCancellationRequested == false) {
                    _Logger.LogError(ex, $"Failed");
                    ++errorCount;

                    if (errorCount > 2) {
                        await Task.Delay(1000 * Math.Min(5, errorCount), stoppingToken);
                    }
                } catch (Exception) when (stoppingToken.IsCancellationRequested == true) {
                    _Logger.LogInformation($"{SERVICE_NAME} stopped by stopping token");
                }
            }
        }

    }
}