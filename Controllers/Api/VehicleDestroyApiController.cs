using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using watchtower.Code.Constants;
using watchtower.Models;
using watchtower.Models.Api;
using watchtower.Models.Census;
using watchtower.Models.Db;
using watchtower.Models.Events;
using watchtower.Services.Db;
using watchtower.Services.Repositories;

namespace watchtower.Controllers.Api {

    [Route("/api/vehicle-destroy")]
    [ApiController]
    public class VehicleDestroyApiController : ApiControllerBase {

        private readonly ILogger<VehicleDestroyApiController> _Logger;

        private readonly VehicleDestroyDbStore _VehicleDestroyDb;
        private readonly SessionDbStore _SessionDb;

        private readonly CharacterRepository _CharacterRepository;
        private readonly VehicleRepository _VehicleRepository;
        private readonly ItemRepository _ItemRepository;
        private readonly ItemCategoryRepository _ItemCategoryRepository;

        public VehicleDestroyApiController(ILogger<VehicleDestroyApiController> logger,
            VehicleDestroyDbStore vehicleDestroyDb, SessionDbStore sessionDb,
            CharacterRepository charRepo, VehicleRepository vehRepo,
            ItemRepository itemRepo, ItemCategoryRepository itemCategoryRepository) {

            _Logger = logger;

            _VehicleDestroyDb = vehicleDestroyDb;
            _SessionDb = sessionDb;
            _CharacterRepository = charRepo;
            _VehicleRepository = vehRepo;
            _ItemRepository = itemRepo;
            _ItemCategoryRepository = itemCategoryRepository;
        }

        /// <summary>
        ///     Get the vehicle destory events that occured during a session
        /// </summary>
        /// <param name="sessionID">ID of the session</param>
        /// <response code="200">
        ///     The response will contain a list of <see cref="ExpandedVehicleDestroyEvent"/>s
        ///     that happening during the <see cref="Session"/> with <see cref="Session.ID"/> of <paramref name="sessionID"/>
        /// </response>
        /// <response code="404">
        ///     No <see cref="Session"/> with <see cref="Session.ID"/> of <paramref name="sessionID"/> exists
        /// </response>
        [HttpGet("session/{sessionID}")]
        public async Task<ApiResponse<List<ExpandedVehicleDestroyEvent>>> GetBySessionID(long sessionID) {
            Session? session = await _SessionDb.GetByID(sessionID);
            if (session == null) {
                return ApiNotFound<List<ExpandedVehicleDestroyEvent>>($"{nameof(Session)} {sessionID}");
            }

            List<VehicleDestroyEvent> events = await _VehicleDestroyDb.GetByCharacterID(session.CharacterID, session.Start, session.End ?? DateTime.UtcNow);

            List<string> charIDs = events.Select(iter => iter.AttackerCharacterID).Union(events.Select(iter => iter.KilledCharacterID)).ToList();

            Dictionary<string, PsCharacter> chars;
            try {
                chars = (await _CharacterRepository.GetByIDs(charIDs, CensusEnvironment.PC, true))
                    .ToDictionary(iter => iter.ID);
            } catch (Exception ex) {
                chars = new Dictionary<string, PsCharacter>();
                _Logger.LogWarning($"failed to get characters for vehicle destroy by session [sessionID={sessionID}] [Exception={ex.Message}]");
            }

            List<ExpandedVehicleDestroyEvent> exs = new(events.Count);
            foreach (VehicleDestroyEvent ev in events) {
                ExpandedVehicleDestroyEvent ex = new();
                ex.Event = ev;

                ex.Attacker = chars.GetValueOrDefault(ev.AttackerCharacterID);
                ex.AttackerVehicle = await _VehicleRepository.GetByID(int.Parse(ev.AttackerVehicleID));

                ex.Killed = chars.GetValueOrDefault(ev.KilledCharacterID);
                ex.KilledVehicle = await _VehicleRepository.GetByID(int.Parse(ev.KilledVehicleID));

                ex.Item = await _ItemRepository.GetByID(ev.AttackerWeaponID);

                exs.Add(ex);
            }

            return ApiOk(exs);
        }

        [HttpGet("session/{sessionID}/block")]
        public async Task<ApiResponse<VehicleKillDeathBlock>> GetBySessionIDBlock(long sessionID) {
            Session? session = await _SessionDb.GetByID(sessionID);
            if (session == null) {
                return ApiNotFound<VehicleKillDeathBlock>($"{nameof(Session)} {sessionID}");
            }

            List<VehicleDestroyEvent> events = await _VehicleDestroyDb.GetByCharacterID(session.CharacterID, session.Start, session.End ?? DateTime.UtcNow);

            VehicleKillDeathBlock block = new();
            block.Kills = events.Where(iter => iter.AttackerCharacterID == session.CharacterID && iter.KilledCharacterID != session.CharacterID).ToList();
            block.Deaths = events.Where(iter => iter.KilledCharacterID == session.CharacterID).ToList();

            // load characters
            List<string> IDs = events.Select(iter => iter.AttackerCharacterID).Distinct().ToList();
            IDs.AddRange(events.Select(iter => iter.KilledCharacterID).Distinct());
            block.Characters = await _CharacterRepository.GetByIDs(IDs, CensusEnvironment.PC);

            // load items
            IEnumerable<int> itemIDs = events.Select(iter => iter.AttackerWeaponID).Distinct();
            block.Weapons = await _ItemRepository.GetByIDs(itemIDs);

            // load item categories
            IEnumerable<int> categoryIDs = block.Weapons.Select(iter => iter.CategoryID).Distinct();
            block.ItemCategories = await _ItemCategoryRepository.GetByIDs(categoryIDs);

            // load vehicles
            List<int> vehicleIDs = events.Select(iter => int.Parse(iter.AttackerVehicleID)).Distinct().ToList();
            vehicleIDs.AddRange(events.Select(iter => int.Parse(iter.KilledVehicleID)).Distinct());
            block.Vehicles = await _VehicleRepository.GetByIDs(vehicleIDs);

            return ApiOk(block);
        }

        /// <summary>
        ///     get the <see cref="VehicleDestroyEvent"/>s where any character in <paramref name="charIDs"/>
        ///     is the <see cref="VehicleDestroyEvent.AttackerCharacterID"/> or <see cref="VehicleDestroyEvent.KilledCharacterID"/>
        ///     between a time period
        /// </summary>
        /// <param name="charIDs">List of character IDs to include. max 50</param>
        /// <param name="start">start of the period to include</param>
        /// <param name="end">end of the period to include. cannot be more than 24 hours</param>
        /// <param name="includeCharacters">if <see cref="VehicleKillDeathBlock.Characters"/> will be populated, defaults to false</param>
        /// <param name="includeVehicles">if <see cref="VehicleKillDeathBlock.Vehicles"/> will be populated, defaults to false</param>
        /// <param name="includeWeapons">if <see cref="VehicleKillDeathBlock.Weapons"/> will be populated, defaults to false</param>
        /// <param name="includeItemCategories">if <see cref="VehicleKillDeathBlock.ItemCategories"/> will be populated, defauls to false</param>
        /// <response code="200">
        ///     the response will contain a <see cref="VehicleKillDeathBlock"/> where <see cref="VehicleKillDeathBlock.Kills"/>
        ///     has a <see cref="VehicleDestroyEvent.AttackerCharacterID"/> within <paramref name="charIDs"/>,
        ///     <see cref="VehicleKillDeathBlock.Deaths"/> has a <see cref="VehicleDestroyEvent.KilledCharacterID"/> within <paramref name="charIDs"/>,
        ///     is between the range of <paramref name="start"/> and <paramref name="end"/>,
        ///     and optionally has <see cref="VehicleKillDeathBlock.Characters"/>, <see cref="VehicleKillDeathBlock.Vehicles"/>,
        ///     <see cref="VehicleKillDeathBlock.Weapons"/> and <see cref="VehicleKillDeathBlock.ItemCategories"/> populated
        ///     depending on the include parameters used
        /// </response>
        /// <response code="400">
        ///     one of the following validation errors occured:
        ///     <ul>
        ///         <li><paramref name="charIDs"/> contained no characters</li>
        ///         <li><paramref name="charIDs"/> had more than 50 characters</li>
        ///         <li><paramref name="start"/> was after <paramref name="end"/></li>
        ///         <li><paramref name="start"/> and <paramref name="end"/> are 24 hours or more apart</li>
        ///     </ul>
        /// </response>
        [HttpGet("characters")]
        public async Task<ApiResponse<VehicleKillDeathBlock>> GetByCharactersInRange(
            [FromQuery] List<string> charIDs,
            [FromQuery] DateTime start, [FromQuery] DateTime end,
            [FromQuery] bool includeCharacters = false,
            [FromQuery] bool includeVehicles = false,
            [FromQuery] bool includeWeapons = false,
            [FromQuery] bool includeItemCategories = false
        ) {

            if (charIDs.Count == 0) {
                return ApiBadRequest<VehicleKillDeathBlock>($"{nameof(charIDs)} must have at least 1 entry");
            }
            if (charIDs.Count > 50) {
                return ApiBadRequest<VehicleKillDeathBlock>($"{nameof(charIDs)} cannot have more than 50 entries");
            }

            if (end - start > TimeSpan.FromDays(1)) {
                return ApiBadRequest<VehicleKillDeathBlock>($"{nameof(start)} and {nameof(end)} cannot have more than a 24 hour difference");
            }
            if (start >= end) {
                return ApiBadRequest<VehicleKillDeathBlock>($"{nameof(start)} must come before ${nameof(end)}");
            }

            List<VehicleDestroyEvent> events = await _VehicleDestroyDb.GetByCharacterIDs(charIDs, start, end);

            VehicleKillDeathBlock block = new();
            block.Kills = events.Where(iter => charIDs.Contains(iter.AttackerCharacterID)).ToList();
            block.Deaths = events.Where(iter => charIDs.Contains(iter.KilledCharacterID)).ToList();

            // load characters
            if (includeCharacters == true) {
                List<string> IDs = events.Select(iter => iter.AttackerCharacterID).Distinct().ToList();
                IDs.AddRange(events.Select(iter => iter.KilledCharacterID).Distinct());
                block.Characters = await _CharacterRepository.GetByIDs(IDs, CensusEnvironment.PC);
            }

            // load items
            if (includeWeapons == true) {
                IEnumerable<int> itemIDs = events.Select(iter => iter.AttackerWeaponID).Distinct();
                block.Weapons = await _ItemRepository.GetByIDs(itemIDs);
            }

            // load item categories
            if (includeItemCategories == true) {
                IEnumerable<int> categoryIDs = block.Weapons.Select(iter => iter.CategoryID).Distinct();
                block.ItemCategories = await _ItemCategoryRepository.GetByIDs(categoryIDs);
            }

            // load vehicles
            if (includeVehicles == true) {
                List<int> vehicleIDs = events.Select(iter => int.Parse(iter.AttackerVehicleID)).Distinct().ToList();
                vehicleIDs.AddRange(events.Select(iter => int.Parse(iter.KilledVehicleID)).Distinct());
                block.Vehicles = await _VehicleRepository.GetByIDs(vehicleIDs);
            }

            return ApiOk(block);
        }

    }
}
