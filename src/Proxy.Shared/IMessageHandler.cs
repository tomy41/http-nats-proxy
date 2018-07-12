﻿using System.Threading.Tasks;

namespace Proxy.Shared
{
    public interface IMessageHandler
    {
        Task<MicroserviceMessage> HandleAsync(MicroserviceMessage msg);
    }
}