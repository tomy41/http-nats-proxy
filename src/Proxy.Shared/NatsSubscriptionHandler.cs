﻿using Microsoft.Extensions.DependencyInjection;
using NATS.Client;
using Serilog;
using System;

namespace Proxy.Shared
{
    /// <summary>
    /// This is a NATS subscription handler that abstracts all of the NATS related logic away from the use case classes. The goal of this class is to
    /// provide the NATS specific code required so that the use case classes can be tested without having to deal with NATS related concerns.
    /// </summary>
    public class NatsSubscriptionHandler
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public NatsSubscriptionHandler(IServiceProvider serviceProvider)
        {
            if (null == serviceProvider) throw new ArgumentNullException(nameof(serviceProvider));

            _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        public async void HandleMsgWith<T>(object sender, MsgHandlerEventArgs e) where T : IMessageHandler
        {
            // store the replyTo subject
            var replyTo = e.Message.Reply;

            // deserialize the microservice msg that was embedded in the nats msg
            var microserviceMsg = e.Message.Data.ToMicroserviceMessage();
            MicroserviceMessage responseMsg = null;

            try
            {
                // create a new scope to handle the message in. this will create a new instance of the handler class per message
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    // create a new instance of the message handler
                    var msgHandler = scope.ServiceProvider.GetService<T>();

                    // handle the message
                    responseMsg = await msgHandler.HandleAsync(microserviceMsg).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                microserviceMsg.ErrorMessage = ex.GetBaseException().Message;
                microserviceMsg.ResponseStatusCode = 500;
                Log.Error(microserviceMsg.ErrorMessage);
            }
            finally
            {
                // send the NATS message (with the response) back to the calling client
                if (null != responseMsg && !string.IsNullOrWhiteSpace(replyTo))
                {
                    NatsHelper.Publish(replyTo, responseMsg);
                }
            }
        }

        public async void ObserveMsgWith<T>(object sender, MsgHandlerEventArgs e) where T : IMessageObserver
        {
            // deserialize the microservice msg that was embedded in the nats msg
            var microserviceMsg = e.Message.Data.ToMicroserviceMessage();

            try
            {
                // create a new scope to handle the message in. this will create a new instance of the observer class per message
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    // create a new instance of the message handler
                    var msgObserver = scope.ServiceProvider.GetService<T>();

                    // observe the message
                    await msgObserver.ObserveAsync(microserviceMsg).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.GetBaseException().Message);
            }
        }
    }
}