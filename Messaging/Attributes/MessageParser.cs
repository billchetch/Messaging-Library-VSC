using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace Chetch.Messaging.Attributes;


[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MessageParser : Attribute
{
    static public Message Parse(Object o, String propsList = null, ParsingPolicy policy = ParsingPolicy.EXCLUDE)
    {
        var msg = new Message();

        var type = o.GetType();
        var props = type.GetProperties();
        var props2check = propsList == null ? [] : propsList.Split(",");
        for(int i = 0; i < props2check.Length; i++)
        {
            props2check[i] = props2check[i].Trim();
        }

        
        foreach(var p in props)
        {
            bool add2msg = p.CanRead;
            if(add2msg && props2check.Length > 0)
            {
                add2msg = policy == ParsingPolicy.EXCLUDE ? !props2check.Contains(p.Name) : props2check.Contains(p.Name);
            }
            if(add2msg)
            {
                
                var mparsers = p.GetCustomAttributes(true).Where(atr => atr is MessageParser);
                foreach(MessageParser mparser in mparsers)
                {
                    if (mparser.Exclude)
                    {
                        add2msg = false;
                        break;
                    }
                }
            }

            if (add2msg)
            {
                msg.AddValue(p.Name, p.GetValue(o));
            }
        }

        return msg;    
    }

    static public Message Parse(MessageType messageType, Object o)
    {
        var msg = Parse(o);
        msg.Type = messageType;
        return msg;
    }

    public enum ParsingPolicy
    {
        INCLUDE,
        EXCLUDE
    }

    public bool Include => Policy == ParsingPolicy.INCLUDE;
    public bool Exclude => Policy == ParsingPolicy.EXCLUDE;

    public ParsingPolicy Policy { get; internal set; } = ParsingPolicy.EXCLUDE; 

    public MessageParser(ParsingPolicy policy)
    {
        Policy = policy;
    }
}
