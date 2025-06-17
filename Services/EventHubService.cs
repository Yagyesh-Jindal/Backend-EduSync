using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System.Text;
using EduSyncAPI.Services;


namespace EduSyncAPI.Services
{
    public class EventHubService
    {
        private readonly EventHubProducerClient _producerClient;

        public EventHubService(IConfiguration config)
        {
            string connectionString = config["EventHub:ConnectionString"];
            string hubName = config["EventHub:HubName"];
            _producerClient = new EventHubProducerClient(connectionString, hubName);
        }

        public async Task SendEventAsync(string message)
        {
            using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync();
            eventBatch.TryAdd(new EventData(Encoding.UTF8.GetBytes(message)));
            await _producerClient.SendAsync(eventBatch);
        }
    }
}
