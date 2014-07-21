using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace PartialFields
{
    /// <summary>
    /// Helper class for processing XML documents.
    /// </summary>
    public class XMLDocumentHelper
    {
        public string GetProcessedXml(string xml, string fields)
        {
            if (String.IsNullOrEmpty(xml))
            {
                return String.Empty;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            if (doc.GetElementsByTagName("ErrorCode").Count != 0)
            {
                return xml;
            }
            string rootNodeName = doc.DocumentElement.Name;

            Field field = new Field(rootNodeName, fields.ToLower());

            XmlNode sourceNode = doc.SelectSingleNode(rootNodeName);

            return GetFilteredXml(sourceNode, field);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        string GetFilteredXml(XmlNode sourceNode, Field field)
        {
            string rootNodeName = sourceNode.Name;
            string targetNodeName = String.Empty;
            XmlDocument outdoc = new XmlDocument();

            XmlElement rootNode = outdoc.CreateElement(rootNodeName);
            foreach (XmlAttribute attribute in sourceNode.Attributes)
            {
                rootNode.SetAttribute(attribute.Name, attribute.Value);
            }

            foreach (XmlNode childNode in sourceNode.ChildNodes)
            {
                XmlNode importNode = null;
                if (childNode.Name != "Metadata")
                {
                    importNode = outdoc.ImportNode(childNode, false);
                    targetNodeName = childNode.Name;
                }
                else
                {
                    importNode = outdoc.ImportNode(childNode, true);
                }
                rootNode.AppendChild(importNode);
            }

            outdoc.AppendChild(rootNode);
            XmlNode targetNode = outdoc.SelectSingleNode(String.Format("{0}/{1}", rootNodeName, targetNodeName));
            sourceNode = sourceNode.SelectSingleNode(targetNodeName);

            if (field.Children.Count > 0)
            {
                foreach (Field child in field.Children)
                {
                    GetFilteredXml(sourceNode, ref targetNode, child);
                }
            }

            if (sourceNode.OwnerDocument.FirstChild.NodeType == XmlNodeType.XmlDeclaration)
            {
                XmlDeclaration dec = sourceNode.OwnerDocument.FirstChild as XmlDeclaration;
                XmlDeclaration xmlDeclaration = outdoc.CreateXmlDeclaration(dec.Version, dec.Encoding, dec.Standalone);
                outdoc.InsertBefore(xmlDeclaration, outdoc.DocumentElement);
            }

            return outdoc.OuterXml;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <param name="tagetNode"></param>
        /// <param name="field"></param>
        void GetFilteredXml(XmlNode sourceNode, ref XmlNode tagetNode, Field field)
        {
            foreach (XmlNode childNode in sourceNode.ChildNodes)
            {
                if (childNode.Name.ToLower() == field.Name || String.IsNullOrEmpty(field.Name))
                {
                    // this node could be single or have multiple child nodes =fields
                    XmlNode newChildNode = tagetNode.OwnerDocument.CreateNode(XmlNodeType.Element, childNode.Name, "");

                    if (((field.Type == FieldType.Collection)) || (field.Children.Count > 0))
                    {
                        // this node we need more depth. so lets go deeper
                        // get child field

                        if (field.Type == FieldType.Collection)
                        {
                            GetFilteredXml(childNode, ref newChildNode, field.Children[0]);
                        }
                        else
                        {
                            foreach (Field child in field.Children)
                            {
                                GetFilteredXml(childNode, ref newChildNode, child);
                            }
                        }
                    }
                    else
                    {
                        // get all what is inside this node added.
                        newChildNode.InnerXml = childNode.InnerXml;
                    }
                    foreach (XmlAttribute attribute in childNode.Attributes)
                    {
                        XmlAttribute newAttribute = newChildNode.OwnerDocument.CreateAttribute(attribute.Name);
                        newAttribute.Value = attribute.Value;
                        newChildNode.Attributes.Append(newAttribute);
                    }
                    tagetNode.AppendChild(newChildNode);
                }
            }
        }
    }

    /// <summary>
    /// Entity class for representing XML node as field.
    /// </summary>
    public class Field : IComparable<Field>, IEquatable<Field>
    {
        /// <summary>
        /// Gets or sets the name of the field
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the list of children fields.
        /// </summary>
        public List<Field> Children { get; set; }

        /// <summary>
        /// Gets or sets the parent field.
        /// </summary>
        public Field Parent { get; set; }

        /// <summary>
        /// Gets or sets the type of field.
        /// </summary>
        public FieldType Type { get; set; }


        /// <summary>
        /// Initializes field object.
        /// </summary>
        /// <param name="fieldString"></param>
        public Field(string fieldName, string ChildfieldString)
        {
            // token format is 
            // field1,field2, field3:group(field31,field32)

            // we can do a a regular expression check before we go ahead.

            this.Name = fieldName;

            int index = 0;

            StringBuilder curFieldName = new StringBuilder();

            Field curField = this;

            this.Children = new List<Field>();

            Field newField = null;

            while (index < ChildfieldString.Length)
            {
                char curChar = ChildfieldString[index];
                curFieldName.Append(curChar);

                switch (curChar)
                {
                    case ',':
                        if (curFieldName.Length > 1)
                        {
                            // fieldname complete
                            curField.Children.Add(new Field(curFieldName.ToString(0, curFieldName.Length - 1)));
                        }
                        curFieldName = new StringBuilder(); // empty
                        break;

                    case ':':
                        newField = new Field(curFieldName.ToString(0, curFieldName.Length - 1));
                        curField.Children.Add(newField);
                        newField.Parent = curField;
                        curField = newField;
                        curField.Type = FieldType.Collection;
                        curFieldName = new StringBuilder(); // empty
                        break;

                    case '(':
                        newField = new Field(curFieldName.ToString(0, curFieldName.Length - 1));
                        curField.Children.Add(newField);
                        newField.Parent = curField;
                        curField = newField;
                        curFieldName = new StringBuilder(); // empty
                        //field1,field2, field3:group(field31,field32)
                        break;
                    case ')':
                        curFieldName = curFieldName.Replace(")", "");
                        if (curFieldName.Length > 0)
                        {
                            // fieldname complete
                            curField.Children.Add(new Field(curFieldName.ToString(0, curFieldName.Length)));
                        }

                        // roll back to parent
                        curField = curField.Parent;
                        if (curField.Type == FieldType.Collection)
                        {
                            // move one level up
                            curField = curField.Parent;
                        }
                        curFieldName = new StringBuilder(); // empty
                        break;
                }
                index++;
            }
            curFieldName = curFieldName.Replace(")", "");
            if (curFieldName.Length > 0)
            {
                curField.Children.Add(new Field(curFieldName.ToString(0, curFieldName.Length)));
            }
        }

        public Field(string name)
        {
            this.Name = name;
            this.Children = new List<Field>();
        }

        public int CompareTo(Field other)
        {
            // If other is not a valid object reference, this instance is greater. 
            if (other == null)
                return 1;
            return (this.Name == other.Name) ? 0 : 1;
        }

        public Field GetChild(string name)
        {
            return this.Children.Find(f => f.Name == name);
        }

        public bool Equals(Field other)
        {
            // If other is not a valid object reference, this instance is greater. 
            if (other == null)
                return false;
            return (this.Name == other.Name);
        }
    }

    /// <summary>
    /// Enumeration for field types.
    /// </summary>
    public enum FieldType
    {
        Simple,
        Collection
    }

}