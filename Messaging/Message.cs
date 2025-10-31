using System.Text;
using System.Text.Json;

namespace Chetch.Messaging
{
    //32 message types
    public enum MessageType
    {
        NOT_SET,
        REGISTER_LISTENER,
        CUSTOM,
        INFO,
        WARNING,
        ERROR,
        PING,
        PING_RESPONSE,
        STATUS_REQUEST,
        STATUS_RESPONSE,
        COMMAND,
        ERROR_TEST,
        ECHO,
        ECHO_RESPONSE,
        CONFIGURE,
        CONFIGURE_RESPONSE,
        RESET,
        INITIALISE,
        DATA,
        CONNECTION_REQUEST,
        CONNECTION_REQUEST_RESPONSE,
        SHUTDOWN,
        SUBSCRIBE,
        UNSUBSCRIBE,
        COMMAND_RESPONSE,
        TRACE,
        NOTIFICATION,
        SUBSCRIBE_RESPONSE,
        INITIALISE_RESPONSE,
        ALERT,
        FINALISE,
        PRESENCE,
        PRESENCE_RESPONSE
    }

    public enum MessageEncoding
    {
        NOT_SET = 0,
        SYSTEM_DEFINED = 1, //the particulars are decided by the system being implemented (e.g. a single byte command)
        XML,
        QUERY_STRING,
        POSITONAL,
        BYTES_ARRAY,
        JSON
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    [Serializable]
    public class MessageValue
    {
        public String Key;
        public Object Value;
    }

    [Serializable]
    public class Message : IMessageQueueItem<Message>
    {
        //headers
        public String ID = String.Empty;
        public String Target = String.Empty; //to help routing to the correct place at the receive end
        public String ResponseID = String.Empty; //normally the ID of the message that was sent requesting a response (e.g. Ping and Ping Response)
        public String Sender = String.Empty; //who sent the message
        public MessageType Type = MessageType.NOT_SET;
        public int SubType;
        public String Signature = String.Empty; //a way to test whether this message is valid or not
        public String Tag = String.Empty; //can be used to follow messages around

        //constitutes the body of the message
        public List<MessageValue> Values = new List<MessageValue>();
        public String Value
        {
            get
            {
                return Values.Count > 0 && HasValue("Value") ? GetString("Value") : null;
            }
            set
            {
                AddValue("Value", value);
            }
        }
        public MessageEncoding DefaultEncoding { get; set; } = MessageEncoding.JSON;

        //Meta stuff
        public DateTime Created { get; internal set; }

        public Message()
        {
            ID = CreateID();
            Type = MessageType.NOT_SET;
        }

        public Message(MessageType type = MessageType.NOT_SET)
        {
            ID = CreateID();
            Type = type;
        }

        public Message(MessageType type, String target)
        {
            ID = CreateID();
            Type = type;
            Target = target;
        }

        public Message(String message, int subType = 0, MessageType type = MessageType.NOT_SET)
        {
            ID = CreateID();
            Value = message;
            SubType = subType;
            Type = type;
        }

        public Message(String message, MessageType type = MessageType.NOT_SET) : this(message, 0, type) { }

        public Message(Message message)
        {
            Target = message.Target;
            ResponseID = message.ResponseID;
            Type = message.Type;
            SubType = message.SubType;
            Sender = message.Sender;
            Tag = message.Tag;
            Value = message.Value;

            foreach (var mv in message.Values)
            {
                AddValue(mv.Key, mv.Value);
            }
        }

        private String CreateID()
        {
            Created = DateTime.Now;
            return System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + "-" + this.GetHashCode() + "-" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
        }

        public void AddValue(String key, Object value)
        {
            var key2cmp = key.ToLower();
            foreach (var v in Values)
            {
                if (v.Key.ToLower() == key2cmp)
                {
                    v.Value = value;
                    return;
                }
            }

            //if here then there is no existing value
            var mv = new MessageValue();
            mv.Key = key;
            mv.Value = value;
            Values.Add(mv);
        }

        public void AddValues(Dictionary<String, Object> vals)
        {
            foreach (var entry in vals)
            {
                AddValue(entry.Key, entry.Value);
            }
        }

        public bool HasValue()
        {
            return HasValue("Value");
        }

        public bool HasValue(String key)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentNullException();
            }

            var key2cmp = key.ToLower();
            foreach (var v in Values)
            {
                if (v.Key.ToLower() == key2cmp)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasValues(params String[] keys)
        {
            foreach (var key in keys)
            {
                if (!HasValue(key)) return false;
            }
            return true;
        }

        public Object GetValue(String key)
        {
            if (key == null || key.Length == 0) return null;

            var key2cmp = key.ToLower();
            foreach (var v in Values)
            {
                if (v.Key.ToLower() == key2cmp)
                {
                    return v.Value;
                }
            }
            throw new Exception("No value found for key " + key);
        }

        public String GetString(String key)
        {
            return GetValue(key).ToString();
        }

        public T Get<T>(String key, T defaultValue = default(T))
        {
            if (!HasValue(key)) return defaultValue;

            var v = GetValue(key);
            if (v is JsonElement)
            {
                return JsonSerializer.Deserialize<T>((JsonElement)v);
            }
            else if (v is ValueType)
            {
                return (T)v;
            }
            else
            {
                throw new Exception(String.Format("Message::Get key {0} returns unsupported type {1}", key, v.GetType().ToString()));
            }
        }

        /*public int GetInt(String key)
        {
            return System.Convert.ToInt32(GetValue(key));
        }

        public long GetLong(String key)
        {
            return System.Convert.ToInt64(GetValue(key));
        }

        public double GetDouble(String key)
        {
            return System.Convert.ToDouble(GetValue(key));
        }

        public byte GetByte(String key)
        {
            return (byte)GetInt(key);
        }

        public bool GetBool(String key)
        {
            var v = GetValue(key);
            if (v is bool) return (bool)v;
            if (v is String) return System.Convert.ToBoolean(GetString(key));
            return GetInt(key) != 0;
        }

        public T GetEnum<T>(String key) where T : struct
        {
            var v = GetValue(key);
            if (v is JsonElement)
            {
                return JsonSerializer.Deserialize<T>((JsonElement)v);
            }
            else
            {
                return (T)Enum.Parse(typeof(T), v.ToString());
            }
        }

        public DateTime GetDateTime(String key)
        {
            String dts = GetString(key);
            if (dts == null || dts == String.Empty)
            {
                return default(DateTime);
            }
            else
            {
                return DateTime.Parse(dts, System.Globalization.CultureInfo.InvariantCulture);
            }

        }*/

        public List<T> GetList<T>(String key)
        {
            Object v = GetValue(key);
            if (v is System.Collections.ArrayList)
            {
                var al = (System.Collections.ArrayList)v;
                return al.Cast<T>().ToList();
            }
            else if (v is System.Collections.Generic.List<T>)
            {
                return (List<T>)v;
            }
            else if (v is JsonElement)
            {
                return Get<List<T>>(key);
            }

            throw new Exception("Cannot convert to List as value is of type " + v.GetType().ToString());
        }

        public void Clear()
        {
            Values.Clear();
        }


        virtual public String GetJSON(Dictionary<String, Object> vals)
        {
            vals.Add("ID", ID);
            vals.Add("ResponseID", ResponseID);
            vals.Add("Target", Target);
            vals.Add("Sender", Sender);
            vals.Add("Type", Type);
            vals.Add("SubType", SubType);
            vals.Add("Tag", Tag);
            vals.Add("Signature", Signature);

            Dictionary<String, Object> body = new Dictionary<String, Object>();
            foreach (var mv in Values)
            {
                var value = mv.Value;
                body.Add(mv.Key, value);
            }
            vals.Add("Body", body);

            return JsonSerializer.Serialize(vals);
        }

        public String Serialize(MessageEncoding encoding = MessageEncoding.JSON)
        {
            String serialized = null;
            serialized = GetJSON(new Dictionary<String, Object>());
            return serialized;
        }

        public static byte[] Serialize(Message message, MessageEncoding encoding)
        {
            var serialized = message.Serialize(encoding);
            return Chetch.Utilities.Convert.ToBytes(serialized);
        }

        public static T Deserialize<T>(String s, MessageEncoding encoding = MessageEncoding.XML) where T : Message, new()
        {
            T t = new T();
            t.OnDeserialize(s, encoding);
            return t;
        }

        //if no type required for deserializing then no need to default to XML as no class type data is parsed for JSON
        public static Message Deserialize(String s, MessageEncoding encoding = MessageEncoding.JSON)
        {
            return Deserialize<Message>(s, encoding);
        }

        public static Message Deserialize(byte[] bytes, MessageEncoding encoding)
        {
            var s = Chetch.Utilities.Convert.ToString(bytes);
            return Deserialize<Message>(s, encoding);
        }

        virtual public void OnDeserialize(String s, MessageEncoding encoding)
        {
            Dictionary<String, Object> vals;
            switch (encoding)
            {
                case MessageEncoding.XML:
                    break;

                case MessageEncoding.JSON:
                    vals = JsonSerializer.Deserialize<Dictionary<String, Object>>(s);
                    AssignValue<String>(ref ID, "ID", vals);
                    AssignValue<String>(ref ResponseID, "ResponseID", vals);
                    AssignValue<String>(ref Target, "Target", vals);
                    AssignValue<String>(ref Sender, "Sender", vals);
                    AssignValue<MessageType>(ref Type, "Type", vals);
                    AssignValue<int>(ref SubType, "SubType", vals);
                    AssignValue<String>(ref Tag, "Tag", vals);
                    AssignValue<String>(ref Signature, "Signature", vals);
                    if (vals.ContainsKey("Body"))
                    {
                        var iterator = ((JsonElement)vals["Body"]).EnumerateObject();
                        while (iterator.MoveNext())
                        {
                            var j = iterator.Current;
                            AddValue(j.Name, j.Value);
                        }
                    }
                    break;

                case MessageEncoding.QUERY_STRING:
                    break;

                case MessageEncoding.BYTES_ARRAY:
                    break;

                default:
                    throw new Exception("Unrecongnised encoding " + encoding);
            }
        }

        public static void AssignValue<T>(ref T p, String key, Dictionary<String, Object> vals)
        {
            if (vals.ContainsKey(key))
            {
                if (p is MessageType)
                {
                    p = (T)(Object)Int32.Parse(vals[key].ToString());
                    vals.Remove(key);
                }
                else
                {
                    Utilities.Convert.AssignValue<T>(ref p, key, vals, true);
                }
            }
        }

        virtual public String ToStringHeader()
        {
            String lf = Environment.NewLine;
            String s = "ID: " + ID + lf;
            s += "Target: " + Target + lf;
            s += "Response ID: " + ResponseID + lf;
            s += "Sender: " + Sender + lf;
            s += "Type: " + Type + lf;
            s += "Sub Type: " + SubType + lf;
            s += "Tag: " + Tag + lf;
            s += "Signature: " + Signature;
            return s;
        }


        private static String _nullOrEmpty(Object o)
        {
            if (o == null)
            {
                return "[null]";
            }
            else if (o.ToString() == String.Empty)
            {
                return "[empty]";
            }
            else
            {
                return o.ToString();
            }
        }

        virtual public String ToStringValues(bool expandLists = false)
        {
            String lf = Environment.NewLine;
            String s = "Values: " + lf;

            foreach (var v in Values)
            {
                if (v.Value is System.Collections.IList && expandLists)
                {
                    s += v.Key + ":" + lf;
                    foreach (var itm in (System.Collections.IList)v.Value)
                    {
                        s += " - " + _nullOrEmpty(itm) + lf;
                    }
                }
                else if (v.Value != null && v.Value.GetType().IsGenericType && v.Value.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    s += v.Key + ":" + lf;

                    //We use json serializer here to avoid type issues as we only want strings (e.g. for display)
                    var serialized = JsonSerializer.Serialize(v.Value);
                    var d = JsonSerializer.Deserialize<Dictionary<String, String>>(serialized);
                    foreach (var kv in d)
                    {
                        s += " - " + kv.Key + " = " + _nullOrEmpty(kv.Value) + lf;
                    }
                }
                else
                {
                    s += v.Key + " = " + _nullOrEmpty(v.Value) + lf;
                }
            }

            return s;
        }

        override public String ToString()
        {
            String lf = Environment.NewLine;
            String s = ToStringHeader();
            s += lf + ToStringValues(false);
            return s;
        }
    }
}
