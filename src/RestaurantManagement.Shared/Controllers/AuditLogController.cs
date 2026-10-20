using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Models;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Shared.Controllers
{
    [ApiController]
    [Route("api/audit-log")]
    [Authorize(Policy = "reports.read")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogReader _reader;

        public AuditLogController(IAuditLogReader reader)
        {
            _reader = reader;
        }

        // GET api/audit-log
        [HttpGet]
        public async Task<ActionResult<AuditLogPageResponse>> GetAuditLog([FromQuery] AuditLogQuery query)
        {
            return Ok(await _reader.GetEntriesAsync(query));
        }
    }
}
