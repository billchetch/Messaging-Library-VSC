using System.Text;
using Chetch.Messaging.Exceptions;
using Chetch.Utilities;

namespace Chetch.Messaging
{
    public class Frame
    {
        #region Clsses and Enums
        public enum FrameSchema
        {
            SMALL_NO_CHECKSUM = 1,      //FrameSchema = 1 byte, Encoding = 1 byte, Payload size = 1 byte, Payload = max 255 bytes
            SMALL_SIMPLE_CHECKSUM,      //FrameSchema = 1 byte, Encoding = 1 byte, Payload size = 1 byte, Payload = max 255 bytes, Checksum = 1 byte
            MEDIUM_NO_CHECKSUM,         //FrameSchema = 1 byte, Encoding = 1 byte, Payload size = 2 bytes, Payload = max 32K bytes
            MEDIUM_SIMPLE_CHECKSUM      //FrameSchema = 1 byte, Encoding = 1 byte, Payload size = 2 bytes, Payload = max 32K bytes, Checksum = 4 bytes
        }

        public enum FrameError
        {
            NO_ERROR = 0,
            NO_DIMENSIONS,
            NO_HEADER,
            NO_PAYLOAD,
            INCOMPLETE_DATA,
            NON_VALID_SCHEMA,
            NON_VALID_ENCODING,
            CHECKSUM_FAILED,
            ADD_TIMEOUT,
            NON_VALID_PAYLOAD_SIZE
        }

        public class FrameDimensions
        {
            public int Schema { get; internal set; } //nb this is the dimension NOT the schema value
            public int Encoding { get; internal set; } //nb this is the dimension NOT the encoding value
            public int PayloadSize { get; internal set; }
            public int Checksum { get; internal set; }
            public int Payload { get; set; } = -1;

            public FrameDimensions(FrameSchema schema)
            {
                Schema = 1; //nb this is the dimension NOT the schema value
                Encoding = 1; //nb this is the dimension NOT the encoding value
                switch (schema)
                {
                    case FrameSchema.SMALL_NO_CHECKSUM:
                    case FrameSchema.SMALL_SIMPLE_CHECKSUM:
                        PayloadSize = 1;
                        Checksum = schema == FrameSchema.SMALL_SIMPLE_CHECKSUM ? 1 : 0;
                        break;

                    case FrameSchema.MEDIUM_NO_CHECKSUM:
                    case FrameSchema.MEDIUM_SIMPLE_CHECKSUM:
                        PayloadSize = 2;
                        Checksum = schema == FrameSchema.MEDIUM_SIMPLE_CHECKSUM ? 2 : 0;
                        break;
                }
            }

            public int SchemaIndex
            {
                get
                {
                    return 0;
                }

            }
            public int EncodingIndex
            {
                get
                {
                    return SchemaIndex + Schema;
                }
            }
            public int PayloadSizeIndex
            {
                get
                {
                    return EncodingIndex + Encoding;
                }
            }
            public int PayloadIndex
            {
                get
                {
                    return PayloadSizeIndex + PayloadSize;
                }
            }
            public int ChecksumIndex
            {
                get
                {
                    if (Checksum <= 0) throw new Exception("There is no checksum for this frame schema");
                    if (Payload <= 0) throw new Exception("Payload dimension has no value");
                    return PayloadIndex + Payload;
                }
            }

            public int Size
            {
                get
                {
                    if (Payload <= 0) return -1;
                    return Schema + Encoding + PayloadSize + Payload + Checksum;
                }
            }
        }
        #endregion

        #region Properties
        public FrameSchema Schema { 
            get 
            {
                return (FrameSchema)_bytes[0];
            } 
            internal set 
            {
                setByteAt((byte)value, 0);
            } 
        }

        public FrameDimensions Dimensions { get; internal set; }
        public MessageEncoding Encoding { 
            get
            {
                return (MessageEncoding)_bytes[1];
            } 
            set 
            { 
                setByteAt((byte)value, 1);
                
            } 
        }

        public int MaxPayload = -1; //

        public byte[] Payload
        {
            get
            {
                if (Dimensions.Payload > 0 && _bytes.Count >= Dimensions.PayloadIndex + Dimensions.Payload)
                {
                    return _bytes.GetRange(Dimensions.PayloadIndex, Dimensions.Payload).ToArray();
                } else
                {
                    return null;
                }
            }

            set
            {
                byte encoding = (byte)Encoding;
                byte schema = (byte)Schema;

                _bytes.Clear();
                _bytes.Add(schema);
                _bytes.Add(encoding);
                byte[] payloadSize = Utilities.Convert.ToBytes(value.Length, Dimensions.PayloadSize);
                _bytes.AddRange(payloadSize);
                for (int i = 0; i < value.Length; i++)
                {
                    _bytes.Add(value[i]);
                }
                Dimensions.Payload = value.Length;
            }
        }

        public bool Complete { get; internal set; } = false;
        #endregion

        #region Events
        public EventHandler<byte[]> FrameComplete;
        #endregion

        #region Fields
        private List<byte> _bytes = new List<byte>();
        private int _addPosition = 0;
        #endregion
       
        #region Czonstructors
        public Frame(FrameSchema schema)
        {
            Schema = schema;
            Dimensions = new FrameDimensions(schema);
        }

        public Frame(FrameSchema schema, MessageEncoding encoding) : this(schema)
        {
            Encoding = encoding;
        }

        public Frame(FrameSchema schema, MessageEncoding encoding, int maxPayload) : this(schema, encoding)
        {
            MaxPayload = maxPayload;
        }
        #endregion

        private void setByteAt(byte b, int idx)
        {
            if(idx < _bytes.Count)
            {
                _bytes[idx] = b;
            } else
            {
                for(int i = _bytes.Count; i <= idx; i++)
                {
                    _bytes.Add(0);
                }
                _bytes[idx] = b;
            }
        }

        private bool addByte(byte b)
        {
            if (Complete)
            {
                throw new Exception("Frame already complete");
            }

            if (_addPosition == 0)
            {
                if(b != (byte)Schema)
                {
                    throw new FrameException(FrameError.NON_VALID_SCHEMA, String.Format("received {0} does not match this frames schema of {1}", b, Schema));
                }
                //Console.WriteLine("Frame schema: {0}", b);
            }
            else if(_addPosition == 1)
            {
                if(b!= (byte)Encoding)
                {   
                    throw new FrameException(FrameError.NON_VALID_ENCODING, String.Format("{0} is not a valid encoding", _bytes[1]));
                }
                //Console.WriteLine("Encoding: {0}", b);
            }
            else if (_addPosition == Dimensions.PayloadIndex)
            {
                Dimensions.Payload = GetInt(Dimensions.PayloadSizeIndex, Dimensions.PayloadSize);
                if(Dimensions.Payload <= 0)
                {   throw new FrameException(FrameError.INCOMPLETE_DATA, "Payload dimensions must be 1 or more");
                }
                else if(MaxPayload > 0 && Dimensions.Payload > MaxPayload)
                {
                    throw new FrameException(FrameError.NON_VALID_PAYLOAD_SIZE, String.Format("Payload size of {0} must be less than or equal to max of {1}", Dimensions.Payload, MaxPayload));
                }
                //Console.WriteLine("Payload size: {0}", Dimensions.Payload);
            }

            //Console.Write("{0},",b);
            setByteAt(b, _addPosition);
            _addPosition++;
            Complete = Dimensions.Payload > 0 && _addPosition == Dimensions.Size;

            return Complete;
        }

        public void Add(byte[] bytes)
        {
            foreach(byte b in bytes)
            {
                Add(b);
            }
        }

        //Add a byte and capture some stuff
        public void Add(byte b, bool reset = true)
        {
            if(addByte(b)){
                //Console.WriteLine("Frame complete!");
                Validate();
                FrameComplete?.Invoke(this, Payload);
                if(reset)Reset();
            }
        }

        public void Reset()
        {
            Complete = false;
            _addPosition = 0;
            FrameSchema schema = Schema;
            byte encoding = _bytes.Count > 1 ? _bytes[1] : (byte)0;
            _bytes.Clear();
            Schema = schema;
            if (encoding > 0) Encoding = (MessageEncoding)encoding;   
        }

        public void Validate()
        {
            if (!Complete)
            {
                throw new FrameException(Frame.FrameError.INCOMPLETE_DATA);
            }
            if (Payload == null || Dimensions.Payload == 0)
            {
                throw new FrameException( Frame.FrameError.NO_PAYLOAD);
            }
            if (_bytes.Count < Dimensions.PayloadIndex)
            {
                throw new FrameException(Frame.FrameError.NO_HEADER);
            }
            
            //confirm checksum
            if (Dimensions.Checksum > 0)
            {
                switch (Schema)
                {
                    case Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM:
                    case Frame.FrameSchema.MEDIUM_SIMPLE_CHECKSUM:
                        int sum = (int)CheckSum.SimpleAddition(_bytes.GetRange(0, Dimensions.ChecksumIndex).ToArray());
                        int csum = GetInt(Dimensions.ChecksumIndex, Dimensions.Checksum);
                        if (sum != csum)
                        {
                            String msg = String.Format("Supplied checksum {0} != {1} calculated checksum", csum, sum);
                            throw new FrameException(Frame.FrameError.CHECKSUM_FAILED, msg);
                        }
                        break;
                }
            }
        }

        public List<byte> GetBytes(bool addChecksum = true)
        {
            if (Dimensions.Payload <= 0) throw new FrameException(FrameError.NO_PAYLOAD);

            if (addChecksum && Dimensions.Checksum > 0)
            {
                switch (Schema)
                {
                    case Frame.FrameSchema.SMALL_SIMPLE_CHECKSUM:
                    case Frame.FrameSchema.MEDIUM_SIMPLE_CHECKSUM:
                        byte[] csum = CheckSum.SimpleAddition(_bytes.GetRange(0, Dimensions.ChecksumIndex).ToArray(), Dimensions.Checksum);
                        for(int i = 0; i < Dimensions.Checksum; i++)
                        {
                            setByteAt(csum[i], Dimensions.ChecksumIndex + i);
                        }
                        break;

                    default:
                        throw new Exception(String.Format("Schema {0} does not containe a checksum yet one tried to be added in Frame.GetBytes", Schema));
                }
            }

            return _bytes;
        }

        public byte GetByte(int index)
        {
            return _bytes[index];
        }

        public int GetInt(int index, int count)
        {
            byte[] bytes = _bytes.GetRange(index, count).ToArray();
            return Chetch.Utilities.Convert.ToInt(bytes);
        }
    }
}
