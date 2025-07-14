using System;
using System.Runtime.Serialization;

namespace Chetch.Messaging;

public interface IMessageQueueItem<M>
{
    abstract static M Deserialize(byte[] bytes, MessageEncoding encoding);

    abstract static byte[] Serialize(M message, MessageEncoding encoding);
}
