using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduSyncAPI.Services;


namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventTestController : ControllerBase
    {
        private readonly EventHubService _eventHubService;

        public EventTestController(EventHubService eventHubService)
        {
            _eventHubService = eventHubService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendTestEvent()
        {
            var eventData = new
            {
                userId = "abc123",
                quizId = "qz456",
                score = 88,
                timestamp = DateTime.UtcNow
            };

            string json = System.Text.Json.JsonSerializer.Serialize(eventData);
            await _eventHubService.SendEventAsync(json);
            return Ok("✅ Event sent to Event Hub");
        }
    }
}