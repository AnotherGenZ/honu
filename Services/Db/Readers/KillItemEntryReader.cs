using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using watchtower.Models.Db;
using watchtower.Models.Events;

namespace watchtower.Services.Db.Readers {

    public class KillItemEntryReader : IDataReader<KillItemEntry> {

        public override KillItemEntry ReadEntry(NpgsqlDataReader reader) {
            KillItemEntry entry = new KillItemEntry();

            entry.ItemID = reader.GetInt32("item_id");
            entry.Kills = reader.GetInt32("kills");
            entry.HeadshotKills = reader.GetInt32("headshots");
            entry.Users = reader.GetInt32("users");

            return entry;
        }

    }
}
