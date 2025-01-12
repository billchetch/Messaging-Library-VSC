namespace Chetch.Messaging.Exceptions
{
    public class FrameException : Exception
    {
        public Frame.FrameError Error { get; internal set; }


        public FrameException(Frame.FrameError error)
        {
            Error = error;
        }

        public FrameException(Frame.FrameError error, string message) : base(message)
        {
            Error = error;
        }
    }
}
