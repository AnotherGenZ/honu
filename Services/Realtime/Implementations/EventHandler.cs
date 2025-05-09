﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using watchtower.Code;
using watchtower.Code.Constants;
using watchtower.Code.ExtensionMethods;
using watchtower.Code.Hubs;
using watchtower.Code.Hubs.Implementations;
using watchtower.Code.Tracking;
using watchtower.Constants;
using watchtower.Models;
using watchtower.Models.Alert;
using watchtower.Models.Census;
using watchtower.Models.Events;
using watchtower.Models.RealtimeAlert;
using watchtower.Models.Queues;
using watchtower.Services.Census;
using watchtower.Services.Db;
using watchtower.Services.Queues;
using watchtower.Services.Realtime;
using watchtower.Services.Repositories;
using watchtower.Models.Discord;
using Microsoft.Extensions.Options;
using watchtower.Models.PSB;
using System.Diagnostics.Metrics;
using watchtower.Services.Metrics;
using System.Diagnostics.Eventing.Reader;

namespace watchtower.Realtime {

    public class EventHandler : IEventHandler {

        private readonly ILogger<EventHandler> _Logger;

        private readonly KillEventDbStore _KillEventDb;
        private readonly ExpEventDbStore _ExpEventDb;
        private readonly SessionRepository _SessionRepository;
        private readonly FacilityControlDbStore _ControlDb;
        private readonly IBattleRankDbStore _BattleRankDb;
        private readonly FacilityPlayerControlDbStore _FacilityPlayerDb;
        private readonly VehicleDestroyDbStore _VehicleDestroyDb;
        private readonly ItemAddedDbStore _ItemAddedDb;
        private readonly AchievementEarnedDbStore _AchievementEarnedDb;
        private readonly AlertDbStore _AlertDb;
        private readonly AlertPlayerDataRepository _ParticipantDataRepository;
        private readonly ContinentLockDbStore _ContinentLockDb;
        private readonly MetagameEventRepository _MetagameRepository;
        private readonly FishCaughtDbStore _FishCaughtDb;

        private readonly CharacterCacheQueue _CacheQueue;
        private readonly SessionStarterQueue _SessionQueue;
        private readonly CharacterUpdateQueue _WeaponQueue;
        private readonly DiscordMessageQueue _MessageQueue;
        private readonly LogoutUpdateBuffer _LogoutQueue;
        private readonly JaegerSignInOutQueue _JaegerQueue;
        private readonly WeaponUpdateQueue _WeaponUpdateQueue;
        private readonly SessionEndQueue _SessionEndQueue;
        private readonly AlertEndQueue _AlertEndQueue;
        private readonly FacilityControlEventProcessQueue _FacilityControlQueue;

        private readonly CharacterRepository _CharacterRepository;
        private readonly MapCollection _MapCensus;
        private readonly ItemRepository _ItemRepository;
        private readonly MapRepository _MapRepository;
        private readonly FacilityRepository _FacilityRepository;
        private readonly ExperienceTypeRepository _ExperienceTypeRepository;
        private readonly VehicleRepository _VehicleRepository;

        private readonly IHubContext<RealtimeMapHub> _MapHub;
        private readonly WorldTagManager _TagManager;

        private readonly RealtimeAlertEventHandler _NexusHandler;
        private readonly RealtimeAlertRepository _MatchRepository;

        private readonly IOptions<JaegerNsaOptions> _NsaOptions;

        private readonly List<string> _Recent;
        private DateTime _MostRecentProcess = DateTime.UtcNow;

        private readonly EventHandlerMetric _Metrics;

        public EventHandler(ILogger<EventHandler> logger,
            KillEventDbStore killEventDb, ExpEventDbStore expDb,
            CharacterCacheQueue cacheQueue, CharacterRepository charRepo,
            SessionStarterQueue sessionQueue, SessionRepository sessionRepository,
            DiscordMessageQueue msgQueue, MapCollection mapColl,
            FacilityControlDbStore controlDb, CharacterUpdateQueue weaponQueue,
            IBattleRankDbStore rankDb, LogoutUpdateBuffer logoutQueue,
            FacilityPlayerControlDbStore fpDb, VehicleDestroyDbStore vehicleDestroyDb,
            ItemRepository itemRepo, MapRepository mapRepo,
            JaegerSignInOutQueue jaegerQueue, FacilityRepository facRepo,
            IHubContext<RealtimeMapHub> mapHub, AlertDbStore alertDb,
            AlertPlayerDataRepository participantDataRepository, WorldTagManager tagManager,
            ItemAddedDbStore itemAddedDb, AchievementEarnedDbStore achievementEarnedDb,
            RealtimeAlertEventHandler nexusHandler, RealtimeAlertRepository matchRepository,
            WeaponUpdateQueue weaponUpdateQueue, IOptions<JaegerNsaOptions> nsaOptions,
            SessionEndQueue sessionEndQueue, ContinentLockDbStore continentLockDb,
            AlertEndQueue alertEndQueue, FacilityControlEventProcessQueue facilityControlQueue,
            MetagameEventRepository metagameRepository, ExperienceTypeRepository experienceTypeRepository,
            VehicleRepository vehicleRepository, EventHandlerMetric metrics, 
            FishCaughtDbStore fishCaughtDb) {

            _Logger = logger;

            _Recent = new List<string>();

            _KillEventDb = killEventDb ?? throw new ArgumentNullException(nameof(killEventDb));
            _ExpEventDb = expDb ?? throw new ArgumentNullException(nameof(expDb));
            _ControlDb = controlDb ?? throw new ArgumentNullException(nameof(controlDb));
            _BattleRankDb = rankDb ?? throw new ArgumentNullException(nameof(rankDb));
            _FacilityPlayerDb = fpDb ?? throw new ArgumentNullException(nameof(fpDb));
            _VehicleDestroyDb = vehicleDestroyDb ?? throw new ArgumentNullException(nameof(vehicleDestroyDb));
            _AlertDb = alertDb ?? throw new ArgumentNullException(nameof(alertDb));
            _ContinentLockDb = continentLockDb;
            _FishCaughtDb = fishCaughtDb;

            _CacheQueue = cacheQueue ?? throw new ArgumentNullException(nameof(cacheQueue));
            _SessionQueue = sessionQueue ?? throw new ArgumentNullException(nameof(sessionQueue));
            _MessageQueue = msgQueue ?? throw new ArgumentNullException(nameof(msgQueue));
            _WeaponQueue = weaponQueue ?? throw new ArgumentNullException(nameof(weaponQueue));
            _LogoutQueue = logoutQueue ?? throw new ArgumentNullException(nameof(logoutQueue));
            _JaegerQueue = jaegerQueue ?? throw new ArgumentNullException(nameof(jaegerQueue));

            _CharacterRepository = charRepo ?? throw new ArgumentNullException(nameof(charRepo));
            _MapCensus = mapColl ?? throw new ArgumentNullException(nameof(mapColl));
            _ItemRepository = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
            _MapRepository = mapRepo ?? throw new ArgumentNullException(nameof(mapRepo));
            _FacilityRepository = facRepo ?? throw new ArgumentNullException(nameof(facRepo));

            _MapHub = mapHub;
            _ParticipantDataRepository = participantDataRepository;
            _TagManager = tagManager;
            _ItemAddedDb = itemAddedDb;
            _AchievementEarnedDb = achievementEarnedDb;
            _SessionRepository = sessionRepository;
            _NexusHandler = nexusHandler;
            _MatchRepository = matchRepository;
            _WeaponUpdateQueue = weaponUpdateQueue;
            _NsaOptions = nsaOptions;
            _SessionEndQueue = sessionEndQueue;
            _AlertEndQueue = alertEndQueue;
            _FacilityControlQueue = facilityControlQueue;
            _MetagameRepository = metagameRepository;
            _ExperienceTypeRepository = experienceTypeRepository;
            _VehicleRepository = vehicleRepository;

            _Metrics = metrics;
            _Metrics.SetEventHandler(this);
        }

        public DateTime MostRecentProcess() {
            return _MostRecentProcess;
        }

        public async Task Process(JToken ev) {
            //using var processTrace = HonuActivitySource.Root.StartActivity("EventProcess");

            string? type = ev.Value<string?>("type");
            //processTrace?.AddTag("type", type);

            // The default == for tokens seems like it's by reference, not value. Since the order of the keys in the JSON
            //      object is fixed and hasn't changed in the last 7 months, this is safe.
            // If the order of keys changes, this method of detecting duplicate events will have to change, as it relies
            //      on the key order being the same for duplicate events
            //
            // For example:
            //      { id: 1, value: "howdy" }
            //      { value: "howdy", id: 1 }
            //  
            //      The strings for these JTokens would be different, but they represent the same object. The current duplicate
            //      event checking would not handle this correctly
            //
            if (_Recent.Contains(ev.ToString())) {
                JToken? payload = ev.SelectToken("payload");
                short? worldID = payload?.Value<short?>("world_id");

                string duptype = type ?? "unknown";
                if (duptype == "serviceMessage") {
                    duptype = payload?.Value<string?>("event_name") ?? duptype;
                }

                _Metrics.RecordDuplicate(duptype, worldID);
                //_Logger.LogError($"Skipping duplicate event {ev}");
                return;
            }

            _Recent.Add(ev.ToString());
            if (_Recent.Count > 50) {
                _Recent.RemoveAt(0);
            }

            if (type == "serviceMessage") {
                JToken? payloadToken = ev.SelectToken("payload");
                if (payloadToken == null) {
                    _Logger.LogWarning($"Missing 'payload' from {ev}");
                    return;
                }

                if (payloadToken.Value<int?>("timestamp") != null) {
                    // somehow, it's possible to get events that are in the future, which can really break some things
                    DateTime timestamp = payloadToken.CensusTimestamp("timestamp");
                    TimeSpan diff = timestamp - DateTime.UtcNow;
                    if (timestamp > _MostRecentProcess && diff <= TimeSpan.FromSeconds(3)) {
                        _MostRecentProcess = timestamp;
                    } else if (diff > TimeSpan.FromSeconds(3)) {
                        _Logger.LogWarning($"larger than 3 second diff! {diff}");
                    }
                }

                string? eventName = payloadToken.Value<string?>("event_name");
                //processTrace?.AddTag("eventName", eventName);

                if (eventName == null) {
                    _Logger.LogWarning($"Missing 'event_name' from {ev}");
                } else if (eventName == "PlayerLogin") {
                    _ProcessPlayerLogin(payloadToken);
                } else if (eventName == "PlayerLogout") {
                    await _ProcessPlayerLogout(payloadToken);
                } else if (eventName == "GainExperience") {
                    await _ProcessExperience(payloadToken);
                } else if (eventName == "Death") {
                    await _ProcessDeath(payloadToken);
                } else if (eventName == "FacilityControl") {
                    _ProcessFacilityControl(payloadToken);
                } else if (eventName == "PlayerFacilityCapture") {
                    _ProcessPlayerCapture(payloadToken);
                } else if (eventName == "PlayerFacilityDefend") {
                    _ProcessPlayerDefend(payloadToken);
                } else if (eventName == "ContinentUnlock") {
                    _ProcessContinentUnlock(payloadToken);
                } else if (eventName == "ContinentLock") {
                    await _ProcessContinentLock(payloadToken);
                } else if (eventName == "BattleRankUp") {
                    await _ProcessBattleRankUp(payloadToken);
                } else if (eventName == "MetagameEvent") {
                    await _ProcessMetagameEvent(payloadToken);
                } else if (eventName == "VehicleDestroy") {
                    await _ProcessVehicleDestroy(payloadToken);
                } else if (eventName == "ItemAdded") {
                    await _ProcessItemAdded(payloadToken);
                } else if (eventName == "AchievementEarned") {
                    await _ProcessAchievementEarned(payloadToken);
                } else if (eventName == "FishScan") {
                    await _ProcessFishScan(payloadToken);
                } else {
                    _Logger.LogWarning($"Untracked event_name: '{eventName}': {payloadToken}");
                }

                _Metrics.RecordEvent(eventName ?? "unknown", payloadToken.Value<short?>("world_id"));
            } else if (type == "heartbeat") {
                //_Logger.LogInformation($"Heartbeat: {ev}");
            } else if (type == "connectionStateChanged") {
                //_Logger.LogInformation($"connectionStateChanged: {ev}");
            } else if (type == "serviceStateChanged") {
                //_Logger.LogInformation($"serviceStateChanged: {ev}");
            } else if (type == "" || type == null) {
                //_Logger.LogInformation($": {ev}");
            } else {
                _Logger.LogWarning($"Unchecked message type: '{type}'");
            }

        }

        private async Task _ProcessVehicleDestroy(JToken payload) {
            //_Logger.LogDebug($"{payload}");

            short attackerLoadoutID = payload.GetInt16("attacker_loadout_id", -1);
            short attackerFactionID = Loadout.GetFaction(attackerLoadoutID);
            // PS4 doesn't send this field
            short attackerTeamID = payload.Value<short?>("attacker_team_id") ?? attackerFactionID;
            short teamID = payload.GetRequiredInt16("team_id");

            VehicleDestroyEvent ev = new() {
                AttackerCharacterID = payload.GetRequiredString("attacker_character_id"),
                AttackerLoadoutID = attackerLoadoutID,
                AttackerVehicleID = payload.GetString("attacker_vehicle_id", "0"),
                AttackerFactionID = attackerFactionID,
                AttackerTeamID = attackerTeamID,
                AttackerWeaponID = payload.GetInt32("attacker_weapon_id", 0),

                KilledCharacterID = payload.GetRequiredString("character_id"),
                KilledFactionID = payload.GetInt16("faction_id", Faction.UNKNOWN),
                KilledVehicleID = payload.GetString("vehicle_id", "0"),
                KilledTeamID = teamID,

                ZoneID = payload.GetZoneID(),
                WorldID = payload.GetWorldID(),
                FacilityID = payload.GetString("facility_id", "0"),
                Timestamp = payload.CensusTimestamp("timestamp")
            };

            if (Logging.WorldIDFilter != null && ev.WorldID != Logging.WorldIDFilter) {
                return;
            }

            if (ev.AttackerCharacterID == "0" && ev.KilledCharacterID == "0") {
                return;
            }

            if (ev.AttackerCharacterID == ev.KilledCharacterID) {
                ev.KilledFactionID = ev.AttackerFactionID;
            }

            CensusEnvironment? plat = CensusEnvironmentHelper.FromWorldID(ev.WorldID);
            if (plat == null) {
                _Logger.LogError($"Failed to get the {nameof(CensusEnvironment)} for world ID {ev.WorldID} in vehicle destroy event");
            }

            lock (CharacterStore.Get().Players) {
                // The default value for Online must be false, else when a new TrackedPlayer is constructed,
                //      the session will never start, as the handler already sees the character online,
                //      so no need to start a new session
                TrackedPlayer attacker = CharacterStore.Get().Players.GetOrAdd(ev.AttackerCharacterID, new TrackedPlayer() {
                    ID = ev.AttackerCharacterID,
                    FactionID = ev.AttackerFactionID,
                    TeamID = ev.AttackerTeamID,
                    Online = false,
                    WorldID = ev.WorldID
                });

                if (attacker.ID != "0" && attacker.Online == false) {
                    if (plat != null) {
                        _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                            CharacterID = attacker.ID,
                            LastEvent = ev.Timestamp,
                            Environment = plat.Value
                        });
                    }
                }

                if (plat != null) {
                    _CacheQueue.Queue(attacker.ID, plat.Value);
                }

                attacker.ZoneID = ev.ZoneID;
                attacker.ProfileID = Profile.GetProfileID(ev.AttackerLoadoutID) ?? 0;

                if (attacker.FactionID == Faction.UNKNOWN) {
                    attacker.FactionID = ev.AttackerFactionID; // If a tracked player was made from a login, no faction ID is given
                    attacker.TeamID = ev.AttackerTeamID;
                }

                if (attacker.PossibleVehicleID != int.Parse(ev.AttackerVehicleID) && ev.AttackerCharacterID != ev.KilledCharacterID) {
                    //_Logger.LogDebug($"updating possible vehicle ID of {attacker.ID} from {attacker.PossibleVehicleID} to {ev.AttackerVehicleID} [cause=got kill in vehicle]");
                }
                attacker.PossibleVehicleID = int.Parse(ev.AttackerVehicleID);
                
                // --------------------------------------------
                // killed character updates

                // See above for why false is used for the Online value, instead of true
                TrackedPlayer killed = CharacterStore.Get().Players.GetOrAdd(ev.KilledCharacterID, new TrackedPlayer() {
                    ID = ev.KilledCharacterID,
                    FactionID = ev.KilledFactionID,
                    TeamID = ev.KilledTeamID,
                    Online = false,
                    WorldID = ev.WorldID
                });

                if (plat != null) {
                    _CacheQueue.Queue(killed.ID, plat.Value);
                }

                // Ensure that 2 sessions aren't started if the attacker and killed are the same
                if (killed.ID != "0" && killed.Online == false && attacker.ID != killed.ID) {
                    if (plat != null) {
                        _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                            CharacterID = killed.ID,
                            LastEvent = ev.Timestamp,
                            Environment = plat.Value
                        });
                    }
                }

                killed.ZoneID = ev.ZoneID;
                if (killed.FactionID == Faction.UNKNOWN) {
                    killed.FactionID = ev.KilledFactionID;
                    killed.TeamID = killed.FactionID;
                }

                // if someone was in a vehicle, that vehicle is now dead
                if (killed.PossibleVehicleID != 0) {
                    //_Logger.LogDebug($"updating possible vehicle ID of {killed.ID} from {killed.PossibleVehicleID} to 0 [cause=vehicle was killed]");
                }
                killed.PossibleVehicleID = 0;

                long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                attacker.LatestEventTimestamp = nowSeconds;
                killed.LatestEventTimestamp = nowSeconds;
            }

            if (World.IsTrackedWorld(ev.WorldID)) {
                await _VehicleDestroyDb.Insert(ev, CancellationToken.None);
            }

            _NexusHandler.HandleVehicleDestroy(ev);

            if (ev.KilledVehicleID == Vehicle.SUNDERER_STR && ev.WorldID == World.Jaeger) {
                List<ExpEvent> possibleEvents = RecentSundererDestroyExpStore.Get().GetList();
                ExpEvent? expEvent = null;
                _Logger.LogDebug($"Bus on Jaeger killed!");

                foreach (ExpEvent exp in possibleEvents) {
                    if (exp.SourceID == ev.AttackerCharacterID && exp.Timestamp == ev.Timestamp) {
                        expEvent = exp;
                        break;
                    }

                    // Easily break in case there are many events to look at
                    if (exp.Timestamp > ev.Timestamp) {
                        break;
                    }
                }

                TrackedNpc? bus = null;

                if (expEvent != null && expEvent.OtherID != "0") {
                    lock (NpcStore.Get().Npcs) {
                        NpcStore.Get().Npcs.TryGetValue(expEvent.OtherID, out bus);
                    }
                }

                new Thread(async () => {
                    try {
                        PsCharacter? attacker = await _CharacterRepository.GetByID(ev.AttackerCharacterID, CensusEnvironment.PC);
                        PsCharacter? owner = (bus != null) ? await _CharacterRepository.GetByID(bus.OwnerID, CensusEnvironment.PC) : null;
                        PsCharacter? killed = await _CharacterRepository.GetByID(ev.KilledCharacterID, CensusEnvironment.PC);
                        PsItem? attackerItem = await _ItemRepository.GetByID(ev.AttackerWeaponID);

                        string msg = $"A bus has been blown up at **{ev.Timestamp:u}** on **{Zone.GetName(ev.ZoneID)}\n**";
                        msg += $"Attacker: **{attacker?.GetDisplayName() ?? $"<missing {ev.AttackerCharacterID}>"}**. Faction: **{Faction.GetName(ev.AttackerFactionID)}**, Team: **{Faction.GetName(ev.AttackerTeamID)}\n**";
                        msg += $"Weapon: **{attackerItem?.Name} ({ev.AttackerWeaponID})\n**";
                        msg += $"Owner: **{killed?.GetDisplayName() ?? $"<missing {ev.KilledCharacterID}>"}**. Faction **{Faction.GetName(ev.KilledFactionID)}**, Team: **{Faction.GetName(ev.KilledTeamID)}\n**";

                        if (bus != null) {
                            msg += $"Owner EXP: {owner?.GetDisplayName() ?? $"<missing {bus.OwnerID}>"}\n";
                            DateTime lastUsed = DateTimeOffset.FromUnixTimeMilliseconds(bus.LatestEventAt).UtcDateTime;

                            msg += $"First spawn at: {bus.FirstSeenAt:u} UTC\n";
                            msg += $"Spawns: {bus.SpawnCount}\n";
                            msg += $"Last used: {(int) (DateTime.UtcNow - lastUsed).TotalSeconds} seconds ago";
                        }

                        _MessageQueue.Queue(new HonuDiscordMessage() {
                            ChannelID = _NsaOptions.Value.ChannelID,
                            GuildID = _NsaOptions.Value.GuildID,
                            Message = msg
                        });
                    } catch (Exception ex) {
                        _Logger.LogError(ex, $"error in background tracked sunderer death handler");
                    }
                }).Start();
            }
        }

        private void _ProcessFacilityControl(JToken payload) {
            FacilityControlEvent ev = new() {
                FacilityID = payload.GetInt32("facility_id", 0),
                DurationHeld = payload.GetInt32("duration_held", 0),
                OutfitID = payload.NullableString("outfit_id"),
                OldFactionID = payload.GetInt16("old_faction_id", 0),
                NewFactionID = payload.GetInt16("new_faction_id", 0),
                ZoneID = payload.GetZoneID(),
                WorldID = payload.GetWorldID(),
                Timestamp = payload.CensusTimestamp("timestamp")
            };

            ushort defID = (ushort) (ev.ZoneID & 0xFFFF);
            ushort instanceID = (ushort) ((ev.ZoneID & 0xFFFF0000) >> 4);

            // Exclude flips that aren't interesting
            if (defID == 95 // A tutorial area
                || defID == 364 // Another tutorial area (0x16C)
                ) {

                return;
            }

            _MapRepository.Set(ev.WorldID, ev.ZoneID, ev.FacilityID, ev.NewFactionID);

            try {
                PsZone? zone = _MapRepository.GetZone(ev.WorldID, ev.ZoneID);
                if (zone != null) {
                    new Thread(async () => {
                        try {
                            await _MapHub.Clients.Group($"RealtimeMap.{ev.WorldID}.{ev.ZoneID}").SendAsync("UpdateMap", zone);
                        } catch (Exception ex) {
                            _Logger.LogError($"failed to send signalR event 'UpdateMap' to RealtimeMap.{ev.WorldID}.{ev.ZoneID}", ex);
                        }
                    }).Start();
                }
            } catch (Exception ex) {
                _Logger.LogError(ex, $"failed to send 'UpdateMap' event to signalR for worldID {ev.WorldID}, zone ID {ev.ZoneID}");
            }

            _NexusHandler.HandleFacilityControl(ev);

            // Set the map repository before we discard server events, such as a continent unlock, to keep the map repo in sync with live
            if (World.IsTrackedWorld(ev.WorldID) == false || ev.OldFactionID == 0 || ev.NewFactionID == 0) {
                return;
            }

            //_Logger.LogTrace($"Facility control: {payload}");

            //_Logger.LogDebug($"CONTROL> {ev.FacilityID} :: {ev.Players}, {ev.OldFactionID} => {ev.NewFactionID}, {ev.WorldID}:{instanceID:X}.{defID:X}, state: {ev.UnstableState}, {ev.Timestamp}");
            //_Logger.LogDebug($"CONTROL> {ev.FacilityID} {ev.OldFactionID} => {ev.NewFactionID}, {ev.WorldID}:{instanceID:X}.{defID:X}");

            new Thread(async () => {
                try {
                    PsFacility? fac = await _FacilityRepository.GetByID(ev.FacilityID);

                    // wait for the player control events to be processed
                    // 2 seconds in case the facility event comes in at like 0.999, and the player events come at 1.000
                    DateTime waitFor = ev.Timestamp + TimeSpan.FromSeconds(2);
                    UnstableState state = _MapRepository.GetUnstableState(ev.WorldID, ev.ZoneID);

                    int waitCount = 0;
                    while (_MostRecentProcess < waitFor) {
                        await Task.Delay(1000);

                        if (waitCount++ > 10) {
                            _Logger.LogError($"Failed to wait for player control events after {waitCount} times. "
                                + $"Wanted to wait for {waitFor:u}, _MostRecentProcess is {_MostRecentProcess:u}");
                            break;
                        }
                    }

                    List<PlayerControlEvent> events;
                    lock (PlayerFacilityControlStore.Get().Events) {
                        // Clean up is handled in a period hosted service
                        events = PlayerFacilityControlStore.Get().Events.Where(iter => {
                            return iter.FacilityID == ev.FacilityID
                                && iter.WorldID == ev.WorldID
                                && iter.ZoneID == ev.ZoneID
                                && (iter.Timestamp == ev.Timestamp
                                    || (iter.Timestamp + TimeSpan.FromSeconds(1)) == ev.Timestamp
                                    || iter.Timestamp == (ev.Timestamp + TimeSpan.FromSeconds(1)
                                ));
                        }).ToList();

                        ev.Players = events.Count;
                    }

                    ev.UnstableState = state;

                    // so why is a queue used when we're already in a background thread?
                    // when a zone closes, there are like 50-70 events at once, and if poorly timed,
                    //      all of them are inserted into db at the same time.
                    // we do not want to use 50-70 connections and starve any other short lived operation,
                    //      so we get all the information we need, then send it to a queue to actually be inserted into the db
                    FacilityControlEventQueueEntry entry = new(ev, events);
                    _FacilityControlQueue.Queue(entry);

                    //_Logger.LogDebug($"had to wait {waitCount} times for {events.Count} events for facility control [Timestamp={ev.Timestamp:u}] [OutfitID={ev.OutfitID}] [FacilityID={ev.FacilityID}]");

                    RecentFacilityControlStore.Get().Add(ev.WorldID, ev);
                } catch (Exception ex) {
                    _Logger.LogError(ex, "error in background thread of control event");
                }
            }).Start();
        }

        private async Task _ProcessBattleRankUp(JToken payload) {
            string charID = payload.GetRequiredString("character_id");
            int rank = payload.GetInt32("battle_rank", 0);
            DateTime timestamp = payload.CensusTimestamp("timestamp");

            await _BattleRankDb.Insert(charID, rank, timestamp);
        }

        private void _ProcessPlayerCapture(JToken payload) {
            PlayerControlEvent ev = new() {
                IsCapture = true,
                CharacterID = payload.GetRequiredString("character_id"),
                FacilityID = payload.GetInt32("facility_id", 0),
                OutfitID = payload.NullableString("outfit_id"),
                WorldID = payload.GetWorldID(),
                ZoneID = payload.GetZoneID(),
                Timestamp = payload.CensusTimestamp("timestamp")
            };

            if (World.IsTrackedWorld(ev.WorldID) == false) {
                return;
            }

            // Inserted into the DB after the facility control event is generated, and the ID is known
            lock (PlayerFacilityControlStore.Get().Events) {
                PlayerFacilityControlStore.Get().Events.Add(ev);
            }
        }

        private void _ProcessPlayerDefend(JToken payload) {
            PlayerControlEvent ev = new() {
                IsCapture = false,
                CharacterID = payload.GetRequiredString("character_id"),
                FacilityID = payload.GetInt32("facility_id", 0),
                OutfitID = payload.NullableString("outfit_id"),
                WorldID = payload.GetWorldID(),
                ZoneID = payload.GetZoneID(),
                Timestamp = payload.CensusTimestamp("timestamp")
            };

            if (World.IsTrackedWorld(ev.WorldID) == false) {
                return;
            }

            // Inserted into the DB after the facility control event is generated, and the ID is known
            lock (PlayerFacilityControlStore.Get().Events) {
                PlayerFacilityControlStore.Get().Events.Add(ev);
            }
        }

        private void _ProcessPlayerLogin(JToken payload) {
            //_Logger.LogTrace($"Processing login: {payload}");

            string? charID = payload.Value<string?>("character_id");
            if (charID == null || charID == "0") {
                return;
            }

            //using Activity? logoutRoot = HonuActivitySource.Root.StartActivity("PlayerLogin");

            DateTime timestamp = payload.CensusTimestamp("timestamp");
            short worldID = payload.GetWorldID();
            if (worldID == World.Jaeger) {
                _JaegerQueue.QueueSignIn(new JaegerSigninoutEntry() {
                    CharacterID = charID,
                    Timestamp = timestamp
                });
            }

            if (Logging.WorldIDFilter != null && worldID != Logging.WorldIDFilter) {
                return;
            }

            CensusEnvironment? env = CensusEnvironmentHelper.FromWorldID(worldID);
            if (env != null) {
                _CacheQueue.Queue(charID, env.Value);
            } else {
                _Logger.LogError($"Failed to get {nameof(CensusEnvironment)} for world ID {worldID} player login");
            }

            TrackedPlayer p;
            lock (CharacterStore.Get().Players) {
                // The FactionID and TeamID are updated as part of caching the character
                p = CharacterStore.Get().Players.GetOrAdd(charID, new TrackedPlayer() {
                    ID = charID,
                    WorldID = worldID,
                    ZoneID = 0,
                    FactionID = Faction.UNKNOWN,
                    TeamID = Faction.UNKNOWN,
                    Online = false
                });

                p.LastLogin = timestamp;
            }

            if (env != null) {
                _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                    CharacterID = p.ID,
                    LastEvent = timestamp,
                    Environment = env.Value
                });
            }

            p.LatestEventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private Task _ProcessPlayerLogout(JToken payload) {
            string? charID = payload.Value<string?>("character_id");
            if (charID == null) {
                return Task.CompletedTask;
            }

            //using Activity? logoutRoot = HonuActivitySource.Root.StartActivity("PlayerLogout");
            //logoutRoot?.AddTag("CharacterID", charID);

            DateTime timestamp = payload.CensusTimestamp("timestamp");

            short worldID = payload.GetWorldID();
            if (worldID == World.Jaeger) {
                _JaegerQueue.QueueSignOut(new JaegerSigninoutEntry() {
                    CharacterID = charID,
                    Timestamp = timestamp
                });
            }

            CensusEnvironment? plat = CensusEnvironmentHelper.FromWorldID(worldID);
            if (plat != null) {
                _CacheQueue.Queue(charID, plat.Value);
            } else {
                _Logger.LogError($"Failed to get {nameof(CensusEnvironment)} for world ID {worldID} in player logout");
            }

            TrackedPlayer? p;
            lock (CharacterStore.Get().Players) {
                _ = CharacterStore.Get().Players.TryGetValue(charID, out p);
            }

            if (p != null) {
                _SessionEndQueue.Queue(new SessionEndQueueEntry() {
                    CharacterID = p.ID,
                    Timestamp = timestamp,
                    SessionID = p.SessionID
                });

                if (World.IsTrackedWorld(p.WorldID)) {
                    // Queue the character for an update
                    // Null if Honu was started when the character was online
                    if (p.LastLogin != null) {
                        // Intentionally discard, we do not care about the result of this
                        _ = _LogoutQueue.Queue(new LogoutBufferEntry() {
                            CharacterID = charID,
                            LoginTime = p.LastLogin.Value
                        });
                    } else {
                        _WeaponQueue.Queue(charID);
                    }

                    /*
                    using (Activity? db = HonuActivitySource.Root.StartActivity("db")) {
                        await _SessionRepository.End(p.ID, timestamp);
                    }
                    */
                }

                // Reset team of the NSO player as they're now offline
                if (p.FactionID == Faction.NS) {
                    p.TeamID = Faction.NS;
                }
            }

            return Task.CompletedTask;
        }

        private async Task _ProcessMetagameEvent(JToken payload) {
            short worldID = payload.GetWorldID();
            uint zoneID = payload.GetZoneID();
            int instanceID = payload.GetInt32("instance_id", 0);
            string metagameEventName = payload.GetString("metagame_event_state_name", "missing");
            int metagameEventID = payload.GetInt32("metagame_event_id", 0);
            DateTime timestamp = payload.CensusTimestamp("timestamp");

            _Logger.LogDebug($"metagame event payload: {payload}");

            // this is pulled out, as we need to lock the zone state store for the duration of this call
            if (MetagameEvent.IsAerialAnomaly(metagameEventID) == false) {
                lock (ZoneStateStore.Get().Zones) {
                    ZoneState? state = ZoneStateStore.Get().GetZone(worldID, zoneID);

                    if (state == null) {
                        state = new ZoneState() {
                            ZoneID = zoneID,
                            WorldID = worldID,
                            IsOpened = true
                        };
                    }

                    if (metagameEventName == "started") {
                        state.AlertStart = timestamp;

                        if (metagameEventID == 234) { // Nexus pre-match
                            RealtimeAlert match = new();
                            match.WorldID = worldID;
                            match.ZoneID = zoneID;
                            match.Timestamp = timestamp;

                            _MatchRepository.Add(match);

                            _Logger.LogDebug($"Started Nexus match at {match.Timestamp:u}, WorldID = {match.WorldID}, ZoneID = {match.ZoneID}");
                        } else if (metagameEventID == 227) { // Nexus match start
                            RealtimeAlert match = _MatchRepository.Get(worldID, zoneID) ?? new() {
                                WorldID = worldID,
                                ZoneID = zoneID,
                                Timestamp = timestamp - TimeSpan.FromMinutes(20)
                            };

                            _Logger.LogDebug($"Nexus match {match.WorldID}-{match.ZoneID} finished prep period at {timestamp:u}, prep started at {match.Timestamp:u}");
                        }

                        TimeSpan? duration = MetagameEvent.GetDuration(metagameEventID);
                        if (duration == null) {
                            _Logger.LogWarning($"Failed to find duration of metagame event {metagameEventID}\n{payload}");
                            state.AlertEnd = state.AlertStart + TimeSpan.FromMinutes(90);
                        } else {
                            state.AlertEnd = state.AlertStart + duration;
                        }
                    } else if (metagameEventName == "ended" || metagameEventName == "canceled") { // ghost bastions are canceled if ended early
                        state.EndAlert();

                        // Continent unlock events are not sent. To check if a continent is open,
                        //      we get the owner of each continent. If there is no owner, then 
                        //      a continent must be open
                        new Thread(async () => {
                            // Ensure census has times to update
                            await Task.Delay(5000);

                            string s = $"ALERT ended in {worldID}, current owners: ";

                            foreach (uint zoneID in Zone.StaticZones) {
                                short? owner = _MapRepository.GetZoneMapOwner(worldID, zoneID);

                                s += $"[{zoneID} => {owner}] ";

                                if (owner == null) {
                                    ZoneStateStore.Get().UnlockZone(worldID, zoneID);
                                }
                            }

                            _Logger.LogDebug(s);
                        }).Start();
                    } else {
                        _Logger.LogError($"Unchecked value of {nameof(metagameEventName)} '{metagameEventName}' when setting up zone state");
                    }

                    ZoneStateStore.Get().SetZone(worldID, zoneID, state);
                }
            }

            _Logger.LogInformation($"METAGAME in world {worldID} zone {zoneID} metagame: {metagameEventName}/{metagameEventID}");

            if (metagameEventName == "started") {
                TimeSpan? duration = MetagameEvent.GetDuration(metagameEventID);
                PsZone? zone = _MapRepository.GetZone(worldID, zoneID);

                if (duration == null) {
                    _Logger.LogWarning($"Failed to find a duration for MetagameEvent {metagameEventID} [worldID={worldID}] [instanceID={instanceID}]");
                }

                PsMetagameEvent? metaEv = null;
                try {
                    metaEv = await _MetagameRepository.GetByID(metagameEventID);
                } catch (Exception ex) {
                    _Logger.LogError(ex, $"failed to get metagame event {metagameEventID}");
                }

                PsAlert alert = new();
                alert.Timestamp = timestamp;
                alert.ZoneID = zoneID;
                alert.WorldID = worldID;
                alert.AlertID = metagameEventID;
                alert.InstanceID = payload.GetInt32("instance_id", 0);
                alert.Duration = ((int?)duration?.TotalSeconds) ?? (60 * 90); // default to 90 minute alerts if unknown
                alert.ZoneFacilityCount = zone?.Facilities.Count ?? 1;

                lock (ZoneStateStore.Get().Zones) {
                    ZoneState? state = ZoneStateStore.Get().GetZone(worldID, zoneID);
                    if (state != null) {
                        state.Alert = alert;
                        state.AlertInfo = metaEv;
                    } else {
                        _Logger.LogWarning($"missing {nameof(ZoneState)} for alert [metagameEventID={metagameEventID}] [worldID={worldID}] [zoneID={zoneID}]");
                    }
                }

                // Find who owns each warpgate in the zone
                if (zone != null) {
                    List<PsFacility> facs = (await _FacilityRepository.GetAll())
                        .Where(iter => iter.ZoneID == alert.ZoneID)
                        .Where(iter => iter.TypeID == 7) // 7 = warpgate
                        .ToList();

                    _Logger.LogDebug($"Found {facs.Count} warpgates in zone {alert.ZoneID} world {alert.WorldID}, finding owners");

                    foreach (PsFacility fac in facs) {
                        PsFacilityOwner? owner = zone.GetFacilityOwner(fac.FacilityID);
                        if (owner != null) {
                            if (owner.Owner == Faction.VS) {
                                alert.WarpgateVS = owner.FacilityID;
                            } else if (owner.Owner == Faction.NC) {
                                alert.WarpgateNC = owner.FacilityID;
                            } else if (owner.Owner == Faction.TR) {
                                alert.WarpgateTR = owner.FacilityID;
                            } else {
                                _Logger.LogWarning($"In alert start, world {alert.WorldID}, zone {alert.ZoneID}: facility {fac.FacilityID} was unowned by 1|2|3, current owner: {owner.Owner}");
                            }
                        } else {
                            _Logger.LogWarning($"In alert start, world {alert.WorldID}, zone {alert.ZoneID}: failed to get owner of {fac.FacilityID}, zone missing facility");
                        }
                    }
                }

                AlertStore.Get().AddAlert(alert);

                if (World.IsTrackedWorld(alert.WorldID)) {
                    try {
                        alert.ID = await _AlertDb.Insert(alert);
                    } catch (Exception ex) {
                        _Logger.LogError(ex, $"Failed to insert alert in {worldID} in zone {zoneID}");
                    }
                }
            } else if (metagameEventName == "ended" || metagameEventName == "canceled") {
                List<PsAlert> alerts = AlertStore.Get().GetAlerts();

                PsAlert? toRemove = null;
                foreach (PsAlert alert in alerts) {
                    if (alert.ZoneID == zoneID && alert.WorldID == worldID) {
                        toRemove = alert;
                        break;
                    }
                    if (alert.InstanceID == instanceID && alert.WorldID == worldID) {
                        toRemove = alert;
                        break;
                    }
                }


                if (toRemove != null && World.IsTrackedWorld(toRemove.WorldID) == false) {
                    return;
                }

                if (toRemove != null) {

                    _Logger.LogInformation($"metagame event ended [alert.ID={toRemove.ID}] [worldID={worldID}] [zoneID={zoneID}] [instanceID={toRemove.InstanceID}]");
                    AlertStore.Get().RemoveByID(toRemove.ID);

                    decimal countVS = payload.GetDecimal("faction_vs", 0m);
                    decimal countNC = payload.GetDecimal("faction_nc", 0m);
                    decimal countTR = payload.GetDecimal("faction_tr", 0m);

                    toRemove.CountVS = (int)countVS;
                    toRemove.CountNC = (int)countNC;
                    toRemove.CountTR = (int)countTR;

                    // Update the winner faction ID
                    decimal winnerCount = 0;
                    short factionID = 0;

                    if (countVS > winnerCount) {
                        winnerCount = countVS;
                        factionID = Faction.VS;
                    }
                    if (countNC > winnerCount) {
                        winnerCount = countNC;
                        factionID = Faction.NC;
                    } 
                    if (countTR > winnerCount) {
                        winnerCount = countTR;
                        factionID = Faction.TR;
                    }

                    if ((countVS == countNC && (factionID == Faction.VS || factionID == Faction.NC)) // VS and NC tied
                        || (countVS == countTR && (factionID == Faction.VS || factionID == Faction.TR)) // VS and TR tied
                        || (countNC == countTR && (factionID == Faction.NC || factionID == Faction.TR)) // NC and TR tied
                        ) {

                        factionID = 0;
                    }

                    toRemove.VictorFactionID = factionID;

                    // Aerial anomalies can end early, update the duration if needed
                    // 2024-10-21: more than just aerial anomalies can end early, not sure why it's restricted to just those
                    //if (MetagameEvent.IsAerialAnomaly(metagameEventID) == true) {
                        TimeSpan duration = timestamp - toRemove.Timestamp;
                        _Logger.LogDebug($"alert ended [displayID={toRemove.WorldID}-{toRemove.InstanceID}] [duration={(int)duration.TotalSeconds} seconds]"
                            + $" [score vs={toRemove.CountVS}] [score nc={toRemove.CountNC}] [score tr={toRemove.CountTR}]");

                        toRemove.Duration = (int)duration.TotalSeconds;
                        await _AlertDb.UpdateByID(toRemove.ID, toRemove);
                    //}

                    // Get the count of each faction if it's not an aerial anomaly, a lockdown alert
                    /* 2024-10-21: i don't remember why honu relies on it's data more than what the game server gives
                    if (MetagameEvent.IsAerialAnomaly(metagameEventID) == false) {
                        PsZone? zone = _MapRepository.GetZone(worldID, zoneID);
                        if (zone != null) {
                            int factionVS = zone.GetFacilities().Where(iter => iter.Owner == Faction.VS).Count();
                            int factionNC = zone.GetFacilities().Where(iter => iter.Owner == Faction.NC).Count();
                            int factionTR = zone.GetFacilities().Where(iter => iter.Owner == Faction.TR).Count();

                            decimal scoreVS = decimal.Round(toRemove.ZoneFacilityCount * countVS / 100);
                            decimal scoreNC = decimal.Round(toRemove.ZoneFacilityCount * countNC / 100);
                            decimal scoreTR = decimal.Round(toRemove.ZoneFacilityCount * countTR / 100);

                            //_Logger.LogDebug($"VS own {factionVS}, have {toRemove.ZoneFacilityCount * countVS / 100}/{scoreVS}");
                            //_Logger.LogDebug($"NC own {factionNC}, have {toRemove.ZoneFacilityCount * countNC / 100}/{scoreNC}");
                            //_Logger.LogDebug($"TR own {factionTR}, have {toRemove.ZoneFacilityCount * countTR / 100}/{scoreTR}");

                            toRemove.CountVS = (int)scoreVS;
                            toRemove.CountNC = (int)scoreNC;
                            toRemove.CountTR = (int)scoreTR;
                            await _AlertDb.UpdateByID(toRemove.ID, toRemove);
                        } else {
                            _Logger.LogWarning($"Cannot assign score for alert {toRemove.WorldID}-{toRemove.InstanceID} (in zone {toRemove.ZoneID}), missing zone");
                        }
                    }
                    */

                    // finally, now that all of the immediate information that could change (such as facility ownership) has been saved
                    //      to the DB, lets queue the alert creation, which includes creating the alert player data and sending Discord alerts
                    _AlertEndQueue.Queue(new AlertEndQueueEntry() {
                        Alert = toRemove
                    });

                    lock (ZoneStateStore.Get().Zones) {
                        ZoneState? state = ZoneStateStore.Get().GetZone(worldID, zoneID);
                        if (state != null) {
                            state.EndAlert();
                        }
                    }
                } else {
                    _Logger.LogWarning($"Failed to find alert to finish for world {worldID} in zone {zoneID}\nCurrent alerts: {string.Join(", ", alerts.Select(iter => $"{iter.WorldID}.{iter.ZoneID}"))}");
                }
            } else {
                _Logger.LogError($"Unchecked value of {nameof(metagameEventName)} '{metagameEventName}'");
            }
        }

        // these events are actually never sent, and looks like they were removed from the docs
        private void _ProcessContinentUnlock(JToken payload) {
            short worldID = payload.GetWorldID();
            uint zoneID = payload.GetZoneID();

            lock (ZoneStateStore.Get().Zones) {
                ZoneState? state = ZoneStateStore.Get().GetZone(worldID, zoneID) ?? new ZoneState() {
                    ZoneID = zoneID,
                    WorldID = worldID,
                };

                state.IsOpened = true;

                ZoneStateStore.Get().SetZone(worldID, zoneID, state);
            }

            //_Logger.LogDebug($"OPENED In world {worldID} zone {zoneID} was opened");
        }

        private async Task _ProcessContinentLock(JToken payload) {
            short worldID = payload.GetWorldID();
            uint zoneID = payload.GetZoneID();

            lock (ZoneStateStore.Get().Zones) {
                ZoneState? state = ZoneStateStore.Get().GetZone(worldID, zoneID);

                if (state == null) {
                    state = new() {
                        ZoneID = zoneID,
                        WorldID = worldID,
                    };
                }

                state.IsOpened = false;
                state.LastLocked = payload.CensusTimestamp("timestamp");

                ZoneStateStore.Get().SetZone(worldID, zoneID, state);
            }

            // don't save tutorial2 zones, as they generate lock events everytime someone finishes the tutorial
            // not useful for our data
            if ((zoneID & 0xFFFF) != Zone.Tutorial2) {
                await _ContinentLockDb.Upsert(new Models.Db.ContinentLockEntry() {
                    ZoneID = zoneID,
                    WorldID = worldID,
                    Timestamp = payload.CensusTimestamp("timestamp")
                });
            }

            _Logger.LogInformation($"continent locked in world {worldID}, zone {zoneID}/{Zone.GetName(zoneID)}");
        }

        private async Task _ProcessDeath(JToken payload) {
            //using Activity? traceDeath = HonuActivitySource.Root.StartActivity("Death");

            string attackerID = payload.Value<string?>("attacker_character_id") ?? "0";
            string charID = payload.Value<string?>("character_id") ?? "0";

            if (attackerID == "0" && charID == "0") {
                _Logger.LogTrace($"why does this exist? {payload}");
                return;
            }

            int timestamp = payload.Value<int?>("timestamp") ?? 0;
            uint zoneID = payload.GetZoneID();

            short attackerLoadoutID = payload.Value<short?>("attacker_loadout_id") ?? -1;
            short loadoutID = payload.Value<short?>("character_loadout_id") ?? -1;

            short attackerTeamID = payload.GetRequiredInt16("attacker_team_id");
            short teamID = payload.GetRequiredInt16("team_id");

            short attackerFactionID = Loadout.GetFaction(attackerLoadoutID);
            short factionID = Loadout.GetFaction(loadoutID);

            KillEvent ev = new KillEvent() {
                AttackerCharacterID = attackerID,
                AttackerLoadoutID = attackerLoadoutID,
                AttackerTeamID = attackerTeamID,
                KilledCharacterID = charID,
                KilledLoadoutID = loadoutID,
                KilledTeamID = teamID,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime,
                WeaponID = payload.GetInt32("attacker_weapon_id", 0),
                WorldID = payload.GetWorldID(),
                ZoneID = payload.GetZoneID(),
                AttackerFireModeID = payload.GetInt32("attacker_fire_mode_id", 0),
                AttackerVehicleID = payload.GetInt32("attacker_vehicle_id", 0),
                IsHeadshot = (payload.Value<string?>("is_headshot") ?? "0") != "0"
            };
            
            if (Logging.WorldIDFilter != null && ev.WorldID != Logging.WorldIDFilter) {
                return;
            }

            //traceDeath?.AddTag("World", ev.WorldID);
            //traceDeath?.AddTag("Zone", ev.ZoneID);

            CensusEnvironment? env = CensusEnvironmentHelper.FromWorldID(ev.WorldID);
            if (env == null) {
                _Logger.LogError($"Missing {nameof(CensusEnvironment)} for world ID {ev.WorldID}");
            }

            if (env != null) { 
                _CacheQueue.Queue(charID, env.Value);
                if (charID != attackerID) { // no need to cache if it's the same
                    _CacheQueue.Queue(attackerID, env.Value);
                }
            }

            //_Logger.LogTrace($"Processing death: {payload}");

            //using Activity? processDeath = HonuActivitySource.Root.StartActivity("process CharacterStore");
            lock (CharacterStore.Get().Players) {
                // The default value for Online must be false, else when a new TrackedPlayer is constructed,
                //      the session will never start, as the handler already sees the character online,
                //      so no need to start a new session
                TrackedPlayer attacker = CharacterStore.Get().Players.GetOrAdd(ev.AttackerCharacterID, new TrackedPlayer() {
                    ID = ev.AttackerCharacterID,
                    FactionID = attackerFactionID,
                    TeamID = ev.AttackerLoadoutID,
                    Online = false,
                    WorldID = ev.WorldID
                });

                if (env != null && attacker.ID != "0" && attacker.Online == false) {
                    _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                        CharacterID = attacker.ID,
                        LastEvent = ev.Timestamp,
                        Environment = env.Value
                    });
                }

                attacker.ZoneID = zoneID;
                attacker.ProfileID = Profile.GetProfileID(ev.AttackerLoadoutID) ?? 0;
                attacker.TeamID = ev.AttackerTeamID;

                if (attacker.FactionID == Faction.UNKNOWN) {
                    attacker.FactionID = attackerFactionID; // If a tracked player was made from a login, no faction ID is given
                }

                // if the attacker is in a vehicle, we're pretty confident about this being correct lol
                attacker.PossibleVehicleID = ev.AttackerVehicleID;

                // --------------------------------------------------
                // update the killer

                // See above for why false is used for the Online value, instead of true
                TrackedPlayer killed = CharacterStore.Get().Players.GetOrAdd(ev.KilledCharacterID, new TrackedPlayer() {
                    ID = ev.KilledCharacterID,
                    FactionID = factionID,
                    TeamID = ev.KilledTeamID,
                    Online = false,
                    WorldID = ev.WorldID
                });

                // Ensure that 2 sessions aren't started if the attacker and killed are the same
                if (env != null && killed.ID != "0" && killed.Online == false && attacker.ID != killed.ID) {
                    _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                        CharacterID = killed.ID,
                        LastEvent = ev.Timestamp,
                        Environment = env.Value
                    });
                }

                killed.ZoneID = zoneID;
                killed.ProfileID = Profile.GetProfileID(ev.KilledLoadoutID) ?? 0;

                if (killed.FactionID == Faction.UNKNOWN) {
                    killed.FactionID = factionID;
                    killed.TeamID = ev.KilledTeamID;
                }

                if (killed.PossibleVehicleID != 0) {
                    //_Logger.LogDebug($"updating possible vehicle ID of {killed.ID} from {killed.PossibleVehicleID} to 0 [cause=vehicle was killed]");
                }
                killed.PossibleVehicleID = 0;

                long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                attacker.LatestEventTimestamp = nowSeconds;

                killed.LatestEventTimestamp = nowSeconds;
                killed.LatestDeath = ev;
            }
            //processDeath?.Stop();

            if (World.IsTrackedWorld(ev.WorldID)) {
                //using Activity? insertDeath = HonuActivitySource.Root.StartActivity("insert");
                ev.ID = await _KillEventDb.Insert(ev);
                //insertDeath?.Stop();

                _WeaponUpdateQueue.Queue(ev.WeaponID); // If a weapon gets a kill, it'll be good to update it's stats at some point
            }

            _NexusHandler.HandleKill(ev);

            //await _TagManager.OnKillHandler(ev);
        }

        private async Task _ProcessExperience(JToken payload) {
            //_Logger.LogInformation($"Processing exp: {payload}");

            string? charID = payload.Value<string?>("character_id");
            if (charID == null || charID == "0") {
                return;
            }

            //using Activity? rootExp = HonuActivitySource.Root.StartActivity("GainExperience");

            Stopwatch timer = Stopwatch.StartNew();

            long queueMs = timer.ElapsedMilliseconds; timer.Restart();

            int expId = payload.GetInt32("experience_id", -1);
            short loadoutId = payload.GetInt16("loadout_id", -1);
            short worldID = payload.GetWorldID();
            int timestamp = payload.Value<int?>("timestamp") ?? 0;
            uint zoneID = payload.GetZoneID();
            string otherID = payload.GetString("other_id", "0");

            short factionID = Loadout.GetFaction(loadoutId);
            short teamID = payload.Value<short?>("team_id") ?? factionID;

            CensusEnvironment? env = CensusEnvironmentHelper.FromWorldID(worldID);
            if (env != null) {
                _CacheQueue.Queue(charID, env.Value);
            } else {
                _Logger.LogError($"Failed to find {nameof(CensusEnvironment)} for world ID {worldID} in exp event");
            }

            long readValuesMs = timer.ElapsedMilliseconds; timer.Restart();

            ExpEvent ev = new ExpEvent() {
                SourceID = charID,
                LoadoutID = loadoutId,
                TeamID = teamID,
                Amount = payload.Value<int?>("amount") ?? 0,
                ExperienceID = expId,
                OtherID = otherID,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime,
                WorldID = worldID,
                ZoneID = zoneID
            };

            if (Logging.WorldIDFilter != null && ev.WorldID != Logging.WorldIDFilter) {
                return;
            }

            long createEventMs = timer.ElapsedMilliseconds; timer.Restart();

            ExperienceType? expType = await _ExperienceTypeRepository.GetByID(ev.ExperienceID);
            if (timer.ElapsedMilliseconds > 100) {
                _Logger.LogWarning($"took longer than 100ms to load exp type from repo [expID={ev.ExperienceID}] [timer={timer.ElapsedMilliseconds}ms]");
            }

            // this is set below in the lock
            TrackedPlayer? otherPlayer = null;

            //using Activity? processExp = HonuActivitySource.Root.StartActivity("process CharacterStore");
            lock (CharacterStore.Get().Players) {
                // Default false for |Online| to ensure a session is started
                TrackedPlayer p = CharacterStore.Get().Players.GetOrAdd(charID, new TrackedPlayer() {
                    ID = charID,
                    FactionID = factionID,
                    TeamID = teamID,
                    Online = false,
                    WorldID = worldID
                });

                if (p.Online == false && env != null) {
                    _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                        CharacterID = p.ID,
                        LastEvent = ev.Timestamp,
                        Environment = env.Value
                    });
                }

                if (expType != null) {
                    // 2024-02-22 TODO: somehow parse out the vehicle name
                    if (expType.AwardTypeID == ExperienceAwardTypes.GUNNER_KILL && p.PossibleVehicleID == 0) {

                        if (p.PossibleVehicleID != -1) {
                            //_Logger.LogDebug($"updating possible vehicle ID of {p.ID} from {p.PossibleVehicleID} to -1 [cause=GUNNER_KILL]");
                        }

                        p.PossibleVehicleID = -1;
                    }
                }

                // ok, so, it might seem reasonable to assume that if a character repairs a vanguard, then they are in a vanguard,
                //      but that's not true cause of prox repair. prox repair IS NOT a different event, so we don't actually know if they
                //      are in a vehicle or not. a possible improvement would be see if the character is repairing multiple different
                //      vehicles within the same time period, but for now, i just want to get something out there
                // so, if we don't already know what vehicle a character is in, just say they are in some vehicle, but we don't know which one
                if (p.PossibleVehicleID == 0 && Experience.IsVehicleRepair(ev.ExperienceID)) {
                    if (p.PossibleVehicleID != -1) {
                        //_Logger.LogDebug($"updating possible vehicle ID of {p.ID} from {p.PossibleVehicleID} to -1 [cause=VEHICLE_REPAIR]");
                    }
                    p.PossibleVehicleID = -1;
                }

                // if they are in a vehicle resupply, it's most likely a sunderer, but could be a galaxy, so only update it to a sunderer
                //      if they ARE NOT in a galaxy
                //
                // TODO: could this be a corsair? does anyone care about those?
                //
                if (p.PossibleVehicleID != Vehicle.GALAXY
                    && (ev.ExperienceID == Experience.VEHICLE_RESUPPLY || ev.ExperienceID == Experience.SQUAD_VEHICLE_RESUPPLY)) {

                    if (p.PossibleVehicleID != Vehicle.SUNDERER) {
                        //_Logger.LogDebug($"updating possible vehicle ID of {p.ID} from {p.PossibleVehicleID} to {Vehicle.SUNDERER} [cause=VEHICLE_RESUPPLY]");
                    }
                    p.PossibleVehicleID = Vehicle.SUNDERER;
                }

                p.LatestEventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                p.ZoneID = zoneID;
                p.TeamID = teamID;
                p.ProfileID = Profile.GetProfileID(ev.LoadoutID) ?? 0;

                if (p.FactionID == Faction.UNKNOWN) {
                    p.FactionID = factionID;
                }

                // this is needed to update revive stats down below
                CharacterStore.Get().Players.TryGetValue(otherID, out otherPlayer);
            }
            //processExp?.Stop();

            long processCharMs = timer.ElapsedMilliseconds; timer.Restart();

            long ID = 0;
            if (World.IsTrackedWorld(ev.WorldID)) {
                ID = await _ExpEventDb.Insert(ev);
            }
            long dbInsertMs = timer.ElapsedMilliseconds; timer.Restart();

            // If this event was a revive, get the latest death of the character who died and set the revived id
            if (ID > 0 && (ev.ExperienceID == Experience.REVIVE || ev.ExperienceID == Experience.SQUAD_REVIVE)
                && otherPlayer != null && otherPlayer.LatestDeath != null) {

                TimeSpan diff = ev.Timestamp - otherPlayer.LatestDeath.Timestamp;

                if (diff > TimeSpan.FromSeconds(50)) {
                    otherPlayer.LatestDeath = null;
                } else {
                    if (World.IsTrackedWorld(ev.WorldID)) {
                        await _KillEventDb.SetRevived(otherPlayer.LatestDeath.ID, ID);
                    }
                }
            } else if ((ev.ExperienceID == Experience.REVIVE || ev.ExperienceID == Experience.SQUAD_REVIVE)
                && (otherPlayer == null || otherPlayer.LatestDeath == null)) {

                //_Logger.LogTrace($"no death for exp {ID}, missing other? {otherPlayer == null}, missing death? {otherPlayer?.LatestDeath == null}");
            }

            long reviveMs = timer.ElapsedMilliseconds; timer.Restart();

            // Track the sundy and how many spawns it has
            if (expId == Experience.SUNDERER_SPAWN_BONUS && otherID != null && otherID != "0") {
                lock (NpcStore.Get().Npcs) {
                    TrackedNpc npc = NpcStore.Get().Npcs.GetOrAdd(otherID, new TrackedNpc() {
                        OwnerID = charID,
                        FirstSeenAt = ev.Timestamp,
                        NpcID = otherID,
                        SpawnCount = 0,
                        Type = NpcType.Sunderer,
                        WorldID = worldID
                    });

                    ++npc.SpawnCount;
                    npc.LatestEventAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                };
            } else if (expId == Experience.GENERIC_NPC_SPAWN && otherID != null && otherID != "0") {
                lock (NpcStore.Get().Npcs) {
                    TrackedNpc npc = NpcStore.Get().Npcs.GetOrAdd(otherID, new TrackedNpc() {
                        OwnerID = charID,
                        FirstSeenAt = ev.Timestamp,
                        NpcID = otherID,
                        SpawnCount = 0,
                        Type = NpcType.Router,
                        WorldID = worldID
                    });

                    ++npc.SpawnCount;
                    npc.LatestEventAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }

            if (expId == Experience.VKILL_SUNDY && worldID == World.Jaeger) {
                RecentSundererDestroyExpStore.Get().Add(ev);
            }

            _NexusHandler.HandleExp(ev);

            long total = queueMs + readValuesMs + createEventMs + processCharMs + dbInsertMs + reviveMs;

            if (total > 100 && Logging.EventProcess == true) {
                _Logger.LogDebug($"Total: {total}\nQueue: {queueMs}, Read: {readValuesMs}, create: {createEventMs}, process: {processCharMs}, DB {dbInsertMs}, revive {reviveMs}");
            }
        }

        private async Task _ProcessItemAdded(JToken payload) {
            ItemAddedEvent ev = new();
            ev.CharacterID = payload.GetRequiredString("character_id");
            ev.Context = payload.GetString("context", "");
            ev.ItemCount = payload.GetInt32("item_count", 0);
            ev.ItemID = payload.GetRequiredInt32("item_id");
            ev.Timestamp = payload.CensusTimestamp("timestamp");
            ev.WorldID = payload.GetWorldID();
            ev.ZoneID = payload.GetZoneID();

            await _ItemAddedDb.Insert(ev);

            lock (CharacterStore.Get().Players) {
                // Default false for |Online| to ensure a session is started
                TrackedPlayer? p = CharacterStore.Get().Players.GetValueOrDefault(ev.CharacterID);
                if (p == null) {
                    return;
                }

                p.LatestEventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                p.ZoneID = ev.ZoneID;
            }

            /*
                {
                    "character_id": "5429269171559531473",
                    "context": "SkillGrantItemLine",
                    "event_name": "ItemAdded",
                    "item_count": "1",
                    "item_id": "6013812",
                    "timestamp": "1659246325",
                    "world_id": "1",
                    "zone_id": "131434"
                }
             */
        }

        private async Task _ProcessAchievementEarned(JToken payload) {
            AchievementEarnedEvent ev = new AchievementEarnedEvent();

            ev.CharacterID = payload.GetRequiredString("character_id");
            ev.Timestamp = payload.CensusTimestamp("timestamp");
            ev.AchievementID = payload.GetRequiredInt32("achievement_id");
            ev.ZoneID = payload.GetZoneID();
            ev.WorldID = payload.GetWorldID();

            await _AchievementEarnedDb.Insert(ev);

            lock (CharacterStore.Get().Players) {
                // Default false for |Online| to ensure a session is started
                TrackedPlayer? p = CharacterStore.Get().Players.GetValueOrDefault(ev.CharacterID);
                if (p == null) {
                    return;
                }

                p.LatestEventTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                p.ZoneID = ev.ZoneID;
            }

            /*
                {
                    "achievement_id": "90020",
                    "character_id": "5429292002801114593",
                    "event_name": "AchievementEarned",
                    "timestamp": "1659246271",
                    "world_id": "1",
                    "zone_id": "4"
                }
            */
        }

        private async Task _ProcessFishScan(JToken payload) {
            /*
                // from PTS data
                {
                    "character_id":"223418335046796049",
                    "event_name":"FishScan",
                    "fish_id":"1",
                    "loadout_id":"10",
                    "team_id":"344", // HUH
                    "timestamp":"1738609454",
                    "world_id":"101",
                    "zone_id":"344"
                }
            */

            FishCaughtEvent ev = new();

            ev.CharacterID = payload.GetRequiredString("character_id");
            ev.WorldID = payload.GetWorldID();
            ev.ZoneID = payload.GetZoneID();
            ev.Timestamp = payload.CensusTimestamp("timestamp");
            ev.FishID = payload.GetRequiredInt32("fish_id");
            ev.TeamID = payload.GetRequiredInt16("team_id");
            ev.LoadoutID = payload.GetRequiredInt16("loadout_id");

            CensusEnvironment? plat = CensusEnvironmentHelper.FromWorldID(ev.WorldID);
            if (plat == null) {
                _Logger.LogError($"Failed to get the {nameof(CensusEnvironment)} for world ID {ev.WorldID} in fish caught event");
            }

            lock (CharacterStore.Get().Players) {
                short factionID = Loadout.GetFaction(ev.LoadoutID);

                // The default value for Online must be false, else when a new TrackedPlayer is constructed,
                //      the session will never start, as the handler already sees the character online,
                //      so no need to start a new session
                TrackedPlayer player = CharacterStore.Get().Players.GetOrAdd(ev.CharacterID, new TrackedPlayer() {
                    ID = ev.CharacterID,
                    FactionID = factionID,
                    TeamID = ev.TeamID,
                    Online = false,
                    WorldID = ev.WorldID
                });

                if (player.ID != "0" && player.Online == false) {
                    if (plat != null) {
                        _SessionQueue.Queue(new CharacterSessionStartQueueEntry() {
                            CharacterID = player.ID,
                            LastEvent = ev.Timestamp,
                            Environment = plat.Value
                        });
                    }
                }

                if (plat != null) {
                    _CacheQueue.Queue(player.ID, plat.Value);
                }

                player.ZoneID = ev.ZoneID;
                player.ProfileID = Profile.GetProfileID(ev.LoadoutID) ?? 0;
                player.TeamID = ev.TeamID;

                if (player.FactionID == Faction.UNKNOWN) {
                    player.FactionID = factionID; // If a tracked player was made from a login, no faction ID is given
                }

                long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                player.LatestEventTimestamp = nowSeconds;
            }

            if (World.IsTrackedWorld(ev.WorldID)) {
                await _FishCaughtDb.Insert(ev, CancellationToken.None);
            }

        }

    }
}
