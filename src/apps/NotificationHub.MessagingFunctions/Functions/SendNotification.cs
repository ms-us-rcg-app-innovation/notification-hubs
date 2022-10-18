using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Builders.Interfaces;
using NotificationHub.Core.FunctionHelpers;
using NotificationHub.Core.Services;
using System.Net;

namespace NotificationHub.MessagingFunctions.Functions
{
    public class SendNotification
    {
        private readonly ILogger _logger;
        private readonly NotificationHubService _hubService;
        private readonly INotificationPayloadBuilder _payloadBuilder;

        public record PushNotification(string Title, string Body, string Platform, string[] Tags);

        public SendNotification(ILogger<SendNotification> logger, NotificationHubService hubService, INotificationPayloadBuilder payloadBuilder)
        {
            _hubService = hubService;
            _payloadBuilder = payloadBuilder;
            _logger = logger;
        }

        [Function(nameof(SendNotification))]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(
                AuthorizationLevel.Function
              , "post"
              , Route = "send-notification")] HttpRequestData request
              , CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sending notification to targeted audiance");

            try
            {
                var notification = await request.ReadFromJsonAsync<PushNotification>();

                if (notification is null)
                {
                    return await request.CreateErrorResponseAsync("Incorrect notification message format");
                }

                var notificationPayload = CreateRawPayload(notification);
                var outcome = await _hubService.SendNotificationAsync(notification.Platform
                                                                    , notificationPayload
                                                                    , cancellationToken
                                                                    , tags: notification.Tags);

                _logger.LogInformation("Message sent to Notification Hub");

                return await request.CreateOkResponseAsync(outcome);

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during function execution time");
                return await request.CreateErrorResponseAsync(e.Message, HttpStatusCode.InternalServerError);
            }
        }


        private string CreateRawPayload(PushNotification notification)
        {
            _payloadBuilder
                .AddTitle(notification.Title)
                .AddBody(notification.Body);

            switch (notification.Platform)
            {
                case "fcm":
                    return _payloadBuilder.BuildAndroidPayload();
                case "aps":
                    return _payloadBuilder.BuildApplePayload();
                default:
                    throw new Exception("Invalid platform");
            }
        }
    }
}
