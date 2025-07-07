using System;
using Chetch.Utilities;

namespace Chetch.Messaging;


public class MessageQueue<T> : DispatchQueue<T>
{
    public const int MESSAGE_QUEUE_WAIT = 100;

    #region Properties
    public Func<byte[], MessageEncoding, T> Deserialize;
    public Func<T, MessageEncoding, byte[]> Serialize;

    public event EventHandler<Exception> ExceptionThrown;
    public event EventHandler<T> MessageEnqueued;

    public event EventHandler<byte[]> MessageDequeued;
    #endregion

    #region Fields
    Frame frame;
    #endregion

    #region Constructors
    public MessageQueue(int messageQueueWait = MESSAGE_QUEUE_WAIT) : base(() => { return true; }, messageQueueWait)
    { }

    public MessageQueue(Frame.FrameSchema schema, MessageEncoding encoding, Func<byte[], MessageEncoding, T> deserialize, int messageQueueWait = MESSAGE_QUEUE_WAIT) : this(messageQueueWait)
    {
        frame = new Frame(schema, encoding);
        Deserialize = deserialize;

        frame.FrameComplete += (sender, payload) =>
        {
            try
            {
                T message = Deserialize(payload, frame.Encoding);
                Enqueue(message);
                MessageEnqueued?.Invoke(this, message);
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, e);
            }
        };
    }


    public MessageQueue(Frame.FrameSchema schema, MessageEncoding encoding, Func<T, MessageEncoding, byte[]> serialize, int messageQueueWait = MESSAGE_QUEUE_WAIT) : this(messageQueueWait)
    {
        frame = new Frame(schema, encoding);
        Serialize = serialize;
    }
    #endregion

    #region Lifecycle
    public override Task Start()
    {
        if (frame != null && Deserialize == null && Serialize == null)
        {
            throw new Exception("Frame specified but no Serilaize or Deserilizse supplied");
        }
        return base.Start();
    }
    #endregion

    #region Methods
    public void Add(byte[] bytes)
    {
        frame.Add(bytes);
    }

    protected override void OnDequeue(T qi)
    {
        base.OnDequeue(qi);
        if (Serialize != null && MessageDequeued != null)
        {
            frame.Payload = Serialize(qi, frame.Encoding);
            MessageDequeued.Invoke(this, frame.GetBytes().ToArray());
        }
    }
    #endregion
}
