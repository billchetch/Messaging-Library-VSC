using System;
using System.CodeDom;
using System.Net.Quic;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;

namespace Chetch.Messaging;

public class MessageIO<T> where T : IMessageQueueItem<T>
{
    #region Constants
    public const int MESSAGE_QUEUE_WAIT = 100;
    #endregion

    #region Events
    public event ErrorEventHandler ExceptionThrown;

    public event EventHandler<T> MessageReceived;

    public event EventHandler<byte[]> MessageDispatched;
    #endregion

    #region Properties
    public T LastMessageReceived { get; internal set; }
    public T LastMessageDispatched { get; internal set; }
    #endregion

    #region Fields
    MessageQueue<T> qin;
    MessageQueue<T> qout;
    #endregion

    #region Constructors
    /// <summary>
    /// Constructor for queues for messages coming in as a byte stream hence the Deserialize function
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="encoding"></param>
    /// <param name="serialize"></param>
    /// <param name="deserialize"></param>
    /// <param name="messageQueueWait"></param>
    public MessageIO(Frame.FrameSchema schema, MessageEncoding encoding, int messageQueueWait = MESSAGE_QUEUE_WAIT)
    {
        qin = new MessageQueue<T>(schema, encoding, messageQueueWait);
        qin.Dequeued += (sender, message) =>
        {
            LastMessageReceived = message;
            MessageReceived?.Invoke(sender, message);
        };

        qout = new MessageQueue<T>(schema, encoding, messageQueueWait);
        qout.MessageDequeued += (sender, eargs) =>
        {
            LastMessageDispatched = eargs.Message;
            MessageDispatched?.Invoke(sender, eargs.Bytes);
        };

    }
    #endregion

    #region Methods
    public void Add(byte[] bytes)
    {
        qin.Add(bytes);    
    }

    public void Inject(T message)
    {
        qin.Enqueue(message);
    }

    public void Add(T message)
    {
        qout.Enqueue(message);
    }

    public void Start()
    {
        qin.Start();
        qout.Start();
    }

    public async Task Stop(bool flush = false)
    {
        await qin.Stop(flush);
        await qout.Stop(flush);
    }
    #endregion
}
