using System;
using Chetch.Utilities;

namespace Chetch.Messaging;


public class MessageQueue<T> : DispatchQueue<T>
{
    public const int MESSAGE_QUEUE_WAIT = 100;

    #region Properties
    public Func<byte[], T> Deserialize;

    public event EventHandler<Exception> ExceptionThrown;
    public event EventHandler<T> MessageEnqueued;
    #endregion

    #region Fields
    Frame frame;
    #endregion

    #region Constructors
    public MessageQueue(int messageQueueWait = MESSAGE_QUEUE_WAIT) : base(() => { return true; }, messageQueueWait)
    { }

    public MessageQueue(Frame.FrameSchema schema, MessageEncoding encoding, Func<byte[], T> deserialize, int messageQueueWait = MESSAGE_QUEUE_WAIT) : this(messageQueueWait)
    {
        frame = new Frame(schema, encoding);
        Deserialize = deserialize;

        frame.FrameComplete += (sender, payload) =>
        {
            try
            {
                T message = Deserialize(payload);
                Enqueue(message);
                MessageEnqueued?.Invoke(this, message);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, e);
            }
        };
    }
    #endregion


    #region Methods
    public void Add(byte[] bytes)
    {
        frame.Add(bytes);
    }
    #endregion
}
