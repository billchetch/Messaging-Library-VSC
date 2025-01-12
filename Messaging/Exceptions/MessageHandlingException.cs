namespace Chetch.Messaging.Exceptions
{
    public class MessageHandlingException : Exception
    {
        new public Message Message { get; set; }
        public string ErrorMessage { get { return base.Message; } }

        public MessageHandlingException(string msg, Message message, Exception e) : base(msg, e)
        {
            Message = message;
        }

        public MessageHandlingException(string msg, Message message) : base(msg)
        {
            Message = message;
        }

        public MessageHandlingException(Message message) : base("no exception message supplied")
        {
            Message = message;
        }

        public MessageHandlingException(Message message, Exception e) : base(e.Message, e)
        {
            Message = message;
        }
    }
}
