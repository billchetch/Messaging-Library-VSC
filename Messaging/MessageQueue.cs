using System;
using System.CodeDom;
using Chetch.Utilities;

namespace Chetch.Messaging;

/// <summary>
/// Designed for queues in two directions: 
/// 1. Messages coming in as a byte stream (hence the mehtod Add(byte[]) to be deserialized and then Enquued
/// 2. Messages to be dequeued and Serialized for sending out as a byte stream.
/// </summary>
public class MessageQueue<T> : DispatchQueue<T> where T : IMessageQueueItem<T>
{
    #region Constants
    public const int MESSAGE_QUEUE_WAIT = 100;
    #endregion

    #region Classes and Enums
    public class EventArgs : System.EventArgs
    {
         public T Message { get; internal set; }
         public byte[] Bytes { get; internal set; }


        public EventArgs(T qi, byte[] bytes)
        {
            Message = qi;
            Bytes = bytes;
        }
    }
    #endregion


    #region Events
    public event ErrorEventHandler ExceptionThrown;

    /// <summary>
    /// Called when a byte stream has been parsed in to a frame and deserialized as a message
    /// </summary>
    public event EventHandler<EventArgs> MessageEnqueued;

    /// <summary>
    /// Called when a message has been dequeued and serialised and added to a frame to be sent out via a byte stream
    /// </summary>
    public event EventHandler<EventArgs> MessageDequeued;
    #endregion

    #region Fields
    Frame frame;
    #endregion

    #region Constructors
    /// <summary>
    /// Constructor for queues for messages coming in as a byte stream hence the Deserialize function
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="encoding"></param>
    /// <param name="deserialize"></param>
    /// <param name="messageQueueWait"></param>
    public MessageQueue(Frame.FrameSchema schema, MessageEncoding encoding, int messageQueueWait = MESSAGE_QUEUE_WAIT) : base(() => true, messageQueueWait)
    {
        frame = new Frame(schema, encoding);
        
        frame.FrameComplete += (sender, payload) =>
        {
            try
            {
                T message = T.Deserialize(payload, frame.Encoding);
                Enqueue(message);
                MessageEnqueued?.Invoke(this, new EventArgs(message, payload));
            }
            catch (Exception e)
            {
                ExceptionThrown?.Invoke(this, new System.IO.ErrorEventArgs(e));
            }
        };


    }
    #endregion

    #region Methods

    public void Add(byte[] bytes)
    {
        try
        {
            frame.Add(bytes);
        }
        catch (Exception e)
        {
            frame.Reset();
            throw;
        }
    }

    protected override void OnDequeue(T qi)
    {
        base.OnDequeue(qi);
        if (MessageDequeued != null)
        {
            frame.Payload = T.Serialize(qi, frame.Encoding);
            MessageDequeued.Invoke(this, new EventArgs(qi, frame.GetBytes().ToArray()));
        }
    }
    #endregion
}
