using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace GitCommands;

[XmlRoot("dictionary")]
public class XmlSerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    where TKey : notnull
{
    #region IXmlSerializable Members

    XmlSchema? IXmlSerializable.GetSchema()
    {
        return null;
    }

    void IXmlSerializable.ReadXml(XmlReader reader)
    {
        XmlSerializer keySerializer = new(typeof(TKey));
        XmlSerializer valueSerializer = new(typeof(TValue));
        var wasEmpty = reader.IsEmptyElement;

        reader.Read();

        if (reader.NodeType == XmlNodeType.XmlDeclaration)
        {
            reader.Read();
        }

        if (wasEmpty || reader.IsEmptyElement)
        {
            return;
        }

        if (reader.ReadToDescendant("item"))
        {
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                reader.ReadStartElement("item");

                reader.ReadStartElement("key");
                var key = (TKey?)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value");
                var value = (TValue?)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadEndElement();
                reader.MoveToContent();

                this[key!] = value!;
            }
        }

        reader.ReadEndElement();
    }

    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
        XmlSerializer keySerializer = new(typeof(TKey));
        XmlSerializer valueSerializer = new(typeof(TValue));

        foreach ((var key, var value) in this.OrderBy(pair => pair.Key))
        {
            writer.WriteStartElement("item");

            writer.WriteStartElement("key");
            keySerializer.Serialize(writer, key);
            writer.WriteEndElement();

            writer.WriteStartElement("value");
            valueSerializer.Serialize(writer, value);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }

    #endregion
}
