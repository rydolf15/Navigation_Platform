using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavigationPlatform.NotificationWorker.Messaging;

public interface ISignalRNotifier
{
    Task NotifyAsync(Guid userId, string eventType, object payload);
}
