using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using watchtower.Code.ExtensionMethods;
using watchtower.Models.Census;
using watchtower.Services.Metrics;

namespace watchtower.Services.Census.Readers {

    public class CensusDirectiveReader : ICensusReader<PsDirective> {
        public CensusDirectiveReader(CensusMetric metrics) : base(metrics) {
        }

        public override PsDirective? ReadEntry(JsonElement token) {
            PsDirective dir = new PsDirective();

            dir.ID = token.GetRequiredInt32("directive_id");
            dir.TreeID = token.GetInt32("directive_tree_id", 0);
            dir.TierID = token.GetInt32("directive_tier_id", 0);
            dir.ObjectiveSetID = token.GetValue<int?>("objective_set_id");
            dir.Name = token.GetChild("name")?.GetString("en", "<missing name>") ?? "<missing name>";
            dir.Description = token.GetChild("description")?.GetString("en", "<missing description>") ?? "<missing description>";
            dir.ImageSetID = token.GetInt32("image_set_id", 0);
            dir.ImageID = token.GetInt32("image_id", 0);

            return dir;
        }

    }
}
