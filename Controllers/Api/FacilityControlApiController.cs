using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using watchtower.Code.Constants;
using watchtower.Models;
using watchtower.Models.Api;
using watchtower.Models.Db;
using watchtower.Models.Events;
using watchtower.Services.Db;

namespace watchtower.Controllers.Api {

    [Route("/api/facility-control")]
    [ApiController]
    public class FacilityControlApiController : ApiControllerBase {

        private readonly ILogger<FacilityControlApiController> _Logger;
        private readonly FacilityControlDbStore _FacilityControlDb;

        public FacilityControlApiController(ILogger<FacilityControlApiController> logger,
            FacilityControlDbStore facilityControlDb) {

            _Logger = logger;
            _FacilityControlDb = facilityControlDb;
        }

        /// <summary>
        ///     get the <see cref="FacilityControlEvent"/>s 
        /// </summary>
        /// <param name="periodStart">filter period start</param>
        /// <param name="periodEnd">filter period end</param>
        /// <param name="zoneID">optional zoneID to filter the results to. leave null to not filter</param>
        /// <param name="worldID">optional world IDs to filter the results to. leave null for all worlds</param>
        /// <param name="playerThreshold">optional players needed at the control event to be included in</param>
        /// <param name="unstableState"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ApiResponse<List<FacilityControlEvent>>> Get(
            [FromQuery] DateTime periodStart,
            [FromQuery] DateTime periodEnd,
            [FromQuery] uint? zoneID = null,
            [FromQuery] List<short>? worldID = null,
            [FromQuery] int? playerThreshold = null,
            [FromQuery] int? unstableState = null
        ) {

            if (periodEnd - periodStart > TimeSpan.FromDays(7)) {
                return ApiBadRequest<List<FacilityControlEvent>>($"{nameof(periodStart)} and {nameof(periodEnd)} cannot have more than a 7 day difference");
            }
            if (periodStart >= periodEnd) {
                return ApiBadRequest<List<FacilityControlEvent>>($"{nameof(periodStart)} must come before {nameof(periodEnd)}");
            }

            FacilityControlOptions parameters = new();
            parameters.ZoneID = zoneID;
            parameters.WorldIDs = worldID ?? new List<short>();
            parameters.PlayerThreshold = playerThreshold ?? 12;
            parameters.PeriodStart = periodStart;
            parameters.PeriodEnd = periodEnd;

            if (unstableState != null) {
                if (Enum.IsDefined(typeof(UnstableState), unstableState.Value) == false) {
                    return ApiBadRequest<List<FacilityControlEvent>>($"{nameof(unstableState)} is an invalid value");
                }

                parameters.UnstableState = (UnstableState)unstableState.Value;
            } else {
                parameters.UnstableState = UnstableState.UNLOCKED;
            }

            List<FacilityControlEvent> entries = await _FacilityControlDb.GetEvents(parameters);

            return ApiOk(entries);
        }

    }
}
