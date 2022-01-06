using System;
using Microsoft.Extensions.DependencyInjection;
using watchtower.Models.Census;
using watchtower.Models.Db;
using watchtower.Models.Events;
using watchtower.Models.Queues;
using watchtower.Models.Report;
using watchtower.Services.Db.Implementations;

namespace watchtower.Services.Db {

    public static class ServiceCollectionExtensionMethods {

        public static void AddHonuDatabasesServices(this IServiceCollection services) {
            services.AddSingleton<OutfitDbStore>();
            services.AddSingleton<IKillEventDbStore, KillEventDbStore>();
            services.AddSingleton<IExpEventDbStore, ExpEventDbStore>();
            services.AddSingleton<CharacterDbStore>();
            services.AddSingleton<IWorldTotalDbStore, WorldTotalDbStore>();
            services.AddSingleton<ItemDbStore>();
            services.AddSingleton<IStaticDbStore<PsItem>, ItemDbStore>();
            services.AddSingleton<ISessionDbStore, SessionDbStore>();
            services.AddSingleton<FacilityControlDbStore>();
            services.AddSingleton<IFacilityDbStore, FacilityDbStore>();
            services.AddSingleton<IMapDbStore, MapDbStore>();
            services.AddSingleton<ICharacterWeaponStatDbStore, CharacterWeaponStatDbStore>();
            services.AddSingleton<IWeaponStatPercentileCacheDbStore, WeaponStatPercentileCacheDbStore>();
            services.AddSingleton<ICharacterHistoryStatDbStore, CharacterHistoryStatDbStore>();
            services.AddSingleton<ICharacterItemDbStore, CharacterItemDbStore>();
            services.AddSingleton<ICharacterStatDbStore, CharacterStatDbStore>();
            services.AddSingleton<IBattleRankDbStore, BattleRankDbStore>();
            services.AddSingleton<ReportDbStore>();
            services.AddSingleton<CharacterMetadataDbStore>();
            services.AddSingleton<LogoutBufferDbStore>();
            services.AddSingleton<FacilityPlayerControlDbStore>();
            services.AddSingleton<CharacterFriendDbStore>();
            services.AddSingleton<DirectiveDbStore>();
            services.AddSingleton<DirectiveTreeDbStore>();
            services.AddSingleton<DirectiveTierDbStore>();
            services.AddSingleton<DirectiveTreeCategoryDbStore>();
            services.AddSingleton<CharacterDirectiveDbStore>();
            services.AddSingleton<CharacterDirectiveTreeDbStore>();
            services.AddSingleton<CharacterDirectiveTierDbStore>();
            services.AddSingleton<CharacterDirectiveObjectiveDbStore>();
            services.AddSingleton<CharacterAchievementDbStore>();

            // Objective
            services.AddSingleton<IStaticDbStore<PsObjective>, ObjectiveDbStore>();
            services.AddSingleton<ObjectiveDbStore>();
            services.AddSingleton<IStaticDbStore<ObjectiveType>, ObjectiveTypeDbStore>();
            services.AddSingleton<ObjectiveTypeDbStore>();
            services.AddSingleton<IStaticDbStore<ObjectiveSet>, ObjectiveSetDbStore>();
            services.AddSingleton<ObjectiveSetDbStore>();

            services.AddSingleton<IStaticDbStore<Achievement>, AchievementDbStore>();
            services.AddSingleton<AchievementDbStore>();
        }

    }

}