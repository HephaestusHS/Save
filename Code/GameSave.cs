using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Heph.Unity.Save
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ExcludeFromSaveAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class SaveNameAttribute : Attribute
    {
        public virtual string Name { get; }
        public SaveNameAttribute(string name) => Name = name;
    }
    public class SaveField
    {
        public string Name { get; set; }
        public object SaveObject { get; set; }
        public SaveField(string name, object saveObject)
        {
            Name = name;
            SaveObject = saveObject;
        }
    }
    public class GameSaveSettings
    {
        public const uint MaxDepth = 64;
        public Encoding Encoding { get; set; }
        public uint Depth { get; set; }
        public bool Overwrite { get; set; }
        public bool AllowSameNameSaveFields { get; set; }
        public bool SaveFields { get; set; }
        public bool IncludeInheritedFields { get; set; }
        public bool IncludeInheritedProperties { get; set; }
        public bool PrependPersistentDataPath { get; set; }
        public bool Indent { get; set; }
        public bool NewLine { get; set; }
        public GameSaveSettings()
        {
            Encoding = Encoding.UTF8;
            Depth = 2u;
            Overwrite = false;
            AllowSameNameSaveFields = false;
            SaveFields = false;
            IncludeInheritedFields = false;
            IncludeInheritedProperties = false;
            PrependPersistentDataPath = true;
            Indent = true;
            NewLine = true;
        }
    }
    public class GameSave
    {
        protected string Path { get; }
        protected string Extension { get; }
        protected List<SaveField> SaveFields { get; }
        public GameSaveSettings Settings { get; }
        public GameSave(string path) : this(path, new GameSaveSettings()) { }
        public GameSave(string path, GameSaveSettings settings)
        {
            Settings = settings;
            Path = path.Replace('\\', '/');
            if (Settings.PrependPersistentDataPath)
            {
                Path = $"{Application.persistentDataPath}/{Path}";
            }
            int extensionStartIndex = path.LastIndexOf('.');
            if (extensionStartIndex == -1) // no file extension provided, use the default format.
            {
                Extension = ".bin";
                Path += Extension;
            }
            else
            {
                Extension = path[extensionStartIndex..];
            }
            SaveFields = new List<SaveField>();
            if (!Settings.Overwrite && File.Exists(Path))
            {
                Load();
            }
        }
        public virtual SaveField this[int index] => GetField(index);
        public virtual object this[string name] => GetObject(name);
        public virtual SaveField GetField(int index) => new SaveField(SaveFields[index].Name, SaveFields[index].SaveObject);
        public virtual object GetObject(string name) => GetObject<object>(name);
        public virtual T GetObject<T>(string name) => (T)GetFieldByName(name, out _)?.SaveObject ?? default;
        public virtual IEnumerable<SaveField> GetAllSaveFields()
        {
            foreach (SaveField saveField in SaveFields)
            {
                yield return new SaveField(saveField.Name, saveField.SaveObject);
            }
        }
        public virtual IEnumerable<object> GetAllObjects() => GetAllObjects<object>();
        public virtual IEnumerable<T> GetAllObjects<T>()
        {
            foreach (SaveField saveField in SaveFields)
            {
                if (saveField.SaveObject is T saveObject)
                {
                    yield return saveObject;
                }
            }
        }
        public virtual void Append(object saveObject, string name) => Append(new SaveField(name, saveObject));
        public virtual void Append(SaveField saveField)
        {
            if (!Settings.AllowSameNameSaveFields && GetFieldByName(saveField.Name, out _) != null)
            {
                throw new ArgumentException($"A save field with the name '{saveField.Name}' already exists.");
            }
            else
            {
                SaveFields.Add(saveField);
            }
        }
        public virtual void Prepend(object saveObject, string name) => Prepend(new SaveField(name, saveObject));
        public virtual void Prepend(SaveField saveField)
        {
            if (!Settings.AllowSameNameSaveFields && GetFieldByName(saveField.Name, out _) != null)
            {
                throw new ArgumentException($"A save field with the name '{saveField.Name}' already exists.");
            }
            else
            {
                SaveFields.Insert(0, saveField);
            }
        }
        public virtual void Insert(object saveObject, string name, int index) => Insert(new SaveField(name, saveObject), index);
        public virtual void Insert(SaveField saveField, int index)
        {
            if (!Settings.AllowSameNameSaveFields && GetFieldByName(saveField.Name, out _) != null)
            {
                throw new ArgumentException($"A save field with the name '{saveField.Name}' already exists.");
            }
            else
            {
                SaveFields.Insert(index, saveField);
            }
        }
        public virtual void Remove(int index) => SaveFields.Remove(SaveFields[index]);
        public virtual void Remove(object saveObject) => Remove(new SaveField(null, saveObject));
        public virtual void Remove(string name) => Remove(new SaveField(name, null));
        public virtual void Remove(SaveField saveField)
        {
            if (GetFieldBySaveable(saveField.SaveObject, out int index) != null)
            {
                Remove(index);
            }
            else if (GetFieldByName(saveField.Name, out index) != null)
            {
                Remove(index);
            }
        }
        public virtual void Modify(object saveObject, string name) => Modify(new SaveField(name, saveObject));
        public virtual void Modify(object saveObject, int index) => SaveFields[index].SaveObject = saveObject;
        public virtual void Modify(SaveField saveField)
        {
            if (GetFieldByName(saveField.Name, out int index) != null)
            {
                Modify(saveField.SaveObject, index);
            }
            else
            {
                Append(saveField);
            }
        }
        public virtual void Clear() => SaveFields.Clear();
        public virtual void SaveChanges()
        {
            switch (Extension)
            {
                case ".bin":
                    WriteBin();
                    break;
                case ".xml":
                    WriteXml();
                    break;
                case ".json":
                    WriteJson();
                    break;
                default:
                    break;
            }
        }
        protected virtual SaveField GetFieldBySaveable(object saveObject, out int index)
        {
            index = -1;
            if (saveObject != null)
            {
                for (int i = 0; i < SaveFields.Count; i++)
                {
                    SaveField saveField = SaveFields[i];
                    if (saveField.SaveObject == saveObject)
                    {
                        index = i;
                        return saveField;
                    }
                }
            }
            return null;
        }
        protected virtual SaveField GetFieldByName(string name, out int index)
        {
            index = -1;
            if (name != null)
            {
                for (int i = 0; i < SaveFields.Count; i++)
                {
                    SaveField saveField = SaveFields[i];
                    if (saveField.Name == name)
                    {
                        index = i;
                        return saveField;
                    }
                }
            }
            return null;
        }
        protected virtual void WriteBin()
        {
            using (FileStream fileStream = File.Open(Path, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream, Settings.Encoding))
                {
                    foreach (SaveField saveField in SaveFields)
                    {
                        WriteObject(saveField.SaveObject, saveField.Name, 0u);
                    }
                    void WriteObject(object obj, string name, uint i, bool isField = false)
                    {
                        Type objectType = obj.GetType();
                        if (i == 0) // obj is the saveField object
                        {
                            binaryWriter.Write($"[{objectType.FullName} {name}]");
                        }
                        else // obj is a property of the saveField object
                        {
                            if (name == null) // list object
                            {
                                binaryWriter.Write($"<[{objectType.FullName}]>");
                            }
                            else
                            {
                                if (isField)
                                {
                                    binaryWriter.Write($"<[({name})]>");
                                }
                                else
                                {
                                    binaryWriter.Write($"<[{name}]>");
                                }
                            }
                        }
                        if (objectType.IsList())
                        {
                            IList list = (IList)obj;
                            foreach (object listObject in list)
                            {
                                Type listObjectType = listObject.GetType();
                                if (listObjectType.IsPrimitiveOrStringOrDecimal())
                                {
                                    binaryWriter.Write($"<{listObjectType.FullName}>");
                                    binaryWriter.Write(listObject.ToString());
                                }
                                else if (Settings.CheckDepth(i))
                                {
                                    WriteObject(listObject, null, i + 1u);
                                }
                            }
                        }
                        else
                        {
                            foreach (PropertyInfo propertyInfo in objectType.GetSaveableProperties(Settings.IncludeInheritedProperties))
                            {
                                if (propertyInfo.IsPrimitive())
                                {
                                    binaryWriter.Write($"<{propertyInfo.GetSaveableName()}>");
                                    binaryWriter.Write(propertyInfo.GetValue(obj).ToString());
                                }
                                else if (Settings.CheckDepth(i))
                                {
                                    WriteObject(propertyInfo.GetValue(obj), propertyInfo.GetSaveableName(), i + 1u);
                                }
                            }
                            if (Settings.SaveFields)
                            {
                                foreach (FieldInfo fieldInfo in objectType.GetSaveableFields(Settings.IncludeInheritedFields))
                                {
                                    if (fieldInfo.IsPrimitive())
                                    {
                                        binaryWriter.Write($"<({fieldInfo.GetSaveableName()})>");
                                        binaryWriter.Write(fieldInfo.GetValue(obj).ToString());
                                    }
                                    else if (Settings.CheckDepth(i))
                                    {
                                        WriteObject(fieldInfo.GetValue(obj), fieldInfo.GetSaveableName(), i + 1u, true);
                                    }
                                }
                            }
                        }
                        binaryWriter.Write("[end object]");
                    }
                }
            }
        }
        protected virtual void WriteXml()
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
            {
                Encoding = Settings.Encoding,
                Async = false,
                Indent = true,
                IndentChars = Settings.Indent ? "\t" : string.Empty,
                NewLineOnAttributes = false,
                NewLineHandling = NewLineHandling.Entitize,
                NewLineChars = Settings.NewLine ? "\n" : string.Empty
            };
            using (FileStream fileStream = File.Open(Path, FileMode.Create, FileAccess.Write))
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(fileStream, xmlWriterSettings))
                {
                    xmlWriter.WriteStartElement("root");
                    foreach (SaveField saveField in SaveFields)
                    {
                        WriteObject(saveField.SaveObject, saveField.Name, 0u);
                    }
                    xmlWriter.WriteEndElement();
                    void WriteObject(object obj, string name, uint i)
                    {
                        Type objectType = obj.GetType();
                        xmlWriter.WriteStartElement("object");
                        xmlWriter.WriteAttributeString("type", objectType.FullName);
                        if (name != null)
                        {
                            xmlWriter.WriteAttributeString("name", name);
                        }
                        if (objectType.IsList())
                        {
                            IList list = (IList)obj;
                            foreach (object listObject in list)
                            {
                                Type listObjectType = listObject.GetType();
                                xmlWriter.WriteStartElement("item");
                                xmlWriter.WriteAttributeString("type", listObjectType.FullName);
                                if (listObjectType.IsPrimitiveOrStringOrDecimal())
                                {
                                    xmlWriter.WriteValue(listObject.ToString());
                                }
                                else if (Settings.CheckDepth(i))
                                {
                                    WriteObject(listObject, null, i + 1u);
                                }
                                xmlWriter.WriteEndElement();
                            }
                        }
                        else
                        {
                            foreach (PropertyInfo propertyInfo in objectType.GetSaveableProperties(Settings.IncludeInheritedProperties))
                            {
                                xmlWriter.WriteStartElement("property");
                                xmlWriter.WriteAttributeString("name", propertyInfo.GetSaveableName());
                                if (propertyInfo.IsPrimitive())
                                {
                                    xmlWriter.WriteValue(propertyInfo.GetValue(obj).ToString());
                                }
                                else if (Settings.CheckDepth(i))
                                {
                                    WriteObject(propertyInfo.GetValue(obj), null, i + 1u);
                                }
                                xmlWriter.WriteEndElement();
                            }
                            if (Settings.SaveFields)
                            {
                                foreach (FieldInfo fieldInfo in objectType.GetSaveableFields(Settings.IncludeInheritedFields))
                                {
                                    xmlWriter.WriteStartElement("field");
                                    xmlWriter.WriteAttributeString("name", fieldInfo.GetSaveableName());
                                    if (fieldInfo.IsPrimitive())
                                    {
                                        xmlWriter.WriteValue(fieldInfo.GetValue(obj).ToString());
                                    }
                                    else if (Settings.CheckDepth(i))
                                    {
                                        WriteObject(fieldInfo.GetValue(obj), null, i + 1u);
                                    }
                                    xmlWriter.WriteEndElement();
                                }
                            }
                        }
                        xmlWriter.WriteEndElement();
                    }
                }
            }
        }
        protected virtual void WriteJson()
        {
            using (FileStream fileStream = File.Open(Path, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter streamWriter = new StreamWriter(fileStream, Settings.Encoding))
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        if (!Settings.NewLine)
                        {
                            streamWriter.NewLine = string.Empty;
                        }
                        if (!Settings.Indent)
                        {
                            jsonWriter.Indentation = 0;
                        }
                        jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;
                        jsonWriter.WriteStartObject();
                        foreach (SaveField saveField in SaveFields)
                        {
                            WriteObject(saveField.SaveObject, saveField.Name, 0u);
                        }
                        jsonWriter.WriteEndObject();
                        void WriteObject(object obj, string name, uint i)
                        {
                            Type objectType = obj.GetType();
                            if (objectType.IsList())
                            {
                                IList list = (IList)obj;
                                jsonWriter.WritePropertyName(name);
                                jsonWriter.WriteStartObject();
                                jsonWriter.WritePropertyName($"type");
                                jsonWriter.WriteValue(objectType.FullName);
                                for (int j = 0; j < list.Count; j++)
                                {
                                    object listObject = list[j];
                                    Type listObjectType = listObject.GetType();
                                    if (listObjectType.IsPrimitiveOrStringOrDecimal())
                                    {
                                        jsonWriter.WritePropertyName($"item {j} {listObjectType.FullName}");
                                        jsonWriter.WriteValue(listObject.ToString());
                                    }
                                    else
                                    {
                                        WriteObject(listObject, $"item {j}", i + 1u);
                                    }
                                }
                                jsonWriter.WriteEndObject();
                            }
                            else
                            {
                                jsonWriter.WritePropertyName(name);
                                jsonWriter.WriteStartObject();
                                jsonWriter.WritePropertyName("type");
                                jsonWriter.WriteValue(objectType.FullName);
                                foreach (PropertyInfo propertyInfo in objectType.GetSaveableProperties(Settings.IncludeInheritedProperties))
                                {
                                    if (propertyInfo.IsPrimitive())
                                    {
                                        jsonWriter.WritePropertyName(propertyInfo.GetSaveableName());
                                        jsonWriter.WriteValue(propertyInfo.GetValue(obj).ToString());
                                    }
                                    else if (Settings.CheckDepth(i))
                                    {
                                        WriteObject(propertyInfo.GetValue(obj), propertyInfo.GetSaveableName(), i + 1u);
                                    }
                                }
                                if (Settings.SaveFields)
                                {
                                    foreach (FieldInfo fieldInfo in objectType.GetSaveableFields(Settings.IncludeInheritedFields))
                                    {
                                        if (fieldInfo.IsPrimitive())
                                        {
                                            jsonWriter.WritePropertyName($"({fieldInfo.GetSaveableName()})");
                                            jsonWriter.WriteValue(fieldInfo.GetValue(obj).ToString());
                                        }
                                        else if (Settings.CheckDepth(i))
                                        {
                                            WriteObject(fieldInfo.GetValue(obj), $"({fieldInfo.GetSaveableName()})", i + 1u);
                                        }
                                    }
                                }
                                jsonWriter.WriteEndObject();
                            }
                        }
                    }
                }
            }
        }
        protected virtual void Load()
        {
            switch (Extension)
            {
                case ".bin":
                    LoadBin();
                    break;
                case ".xml":
                    LoadXml();
                    break;
                case ".json":
                    LoadJson();
                    break;
                default:
                    throw new ArgumentException($"The file format \"{Extension}\" is not supported. See the documentation for a list of supported formats.");
            }
        }
        protected virtual void LoadBin()
        {
            using (FileStream fileStream = File.Open(Path, FileMode.Open))
            {
                using (BinaryReader binaryReader = new BinaryReader(fileStream, Settings.Encoding))
                {
                    while (fileStream.Position < fileStream.Length)
                    {
                        string[] objectValues = binaryReader.ReadString()[1..^1].Split(' ');
                        string name = objectValues[1];
                        if (objectValues.Length > 2)
                        {
                            foreach (string s in objectValues[2..])
                            {
                                name += " " + s;
                            }
                        }
                        SaveFields.Add(new SaveField(name, LoadObject(Type.GetType(objectValues[0]), 0u)));
                    }
                    object LoadObject(Type objectType, uint i)
                    {
                        object obj = Activator.CreateInstance(objectType);
                        string memberName = null;
                        while (true)
                        {
                            string value = binaryReader.ReadString();
                            if (value == "[end object]")
                            {
                                break;
                            }
                            if (value[0] == '<' && value[^1] == '>') // property
                            {
                                if (value[1] == '[') // type of the property is not primitive.
                                {
                                    if (Settings.CheckDepth(i))
                                    {
                                        if (objectType.IsList())
                                        {
                                            IList list = (IList)obj;
                                            list.Add(LoadObject(Type.GetType(value[2..^2]), i + 1u));
                                        }
                                        else
                                        {
                                            memberName = value[2..^2];
                                            if (memberName[0] == '(' && memberName[^1] == ')')
                                            {
                                                FieldInfo fieldInfo = objectType.GetSaveableField(memberName[1..^1], Settings.IncludeInheritedFields);
                                                fieldInfo.SetValue(obj, LoadObject(fieldInfo.FieldType, i + 1u));
                                            }
                                            else
                                            {
                                                PropertyInfo propertyInfo = objectType.GetSaveableProperty(memberName, Settings.IncludeInheritedProperties);
                                                propertyInfo.SetValue(obj, LoadObject(propertyInfo.PropertyType, i + 1u));
                                            }
                                            memberName = null;
                                        }
                                    }
                                }
                                else
                                {
                                    memberName = value[1..^1];
                                }
                            }
                            else // value
                            {
                                if (objectType.IsList())
                                {
                                    IList list = (IList)obj;
                                    list.Add(Convert.ChangeType(value, Type.GetType(memberName)));
                                }
                                else if (memberName != null)
                                {
                                    if (memberName[0] == '(' && memberName[^1] == ')')
                                    {
                                        FieldInfo fieldInfo = objectType.GetSaveableField(memberName[1..^1], Settings.IncludeInheritedFields);
                                        fieldInfo.SetValue(obj, Convert.ChangeType(value, fieldInfo.FieldType));
                                    }
                                    else
                                    {
                                        PropertyInfo propertyInfo = objectType.GetSaveableProperty(memberName, Settings.IncludeInheritedProperties);
                                        propertyInfo.SetValue(obj, Convert.ChangeType(value, propertyInfo.PropertyType));
                                    }
                                    memberName = null;
                                }
                            }
                        }
                        return obj;
                    }
                }
            }
        }
        protected virtual void LoadXml()
        {
            XDocument xmlFile = XDocument.Load(Path);
            XElement root = xmlFile.Element("root");
            foreach (XElement obj in root.Elements("object"))
            {
                SaveFields.Add(new SaveField(obj.Attribute("name").Value, LoadObject(obj, 0u)));
            }
            object LoadObject(XElement objectElement, uint i)
            {
                Type objectType = Type.GetType(objectElement.Attribute("type").Value);
                object obj = Activator.CreateInstance(objectType);
                if (objectType.IsList())
                {
                    IList list = (IList)obj;
                    foreach (XElement item in objectElement.Elements("item"))
                    {
                        XElement innerObject = item.Element("object");
                        Type listObjectType = Type.GetType(item.Attribute("type").Value);
                        object listObject = Activator.CreateInstance(listObjectType);
                        if (innerObject != null && Settings.CheckDepth(i))
                        {
                            listObject = LoadObject(innerObject, i + 1u);
                        }
                        else
                        {
                            listObject = Convert.ChangeType(item.Value, Type.GetType(item.Attribute("type").Value));
                        }
                        list.Add(listObject);
                    }
                }
                else
                {
                    foreach (XElement property in objectElement.Elements("property"))
                    {
                        XElement innerObject = property.Element("object");
                        PropertyInfo propertyInfo = objectType.GetSaveableProperty(property.Attribute("name").Value, Settings.IncludeInheritedProperties);
                        if (innerObject != null && Settings.CheckDepth(i))
                        {
                            propertyInfo.SetValue(obj, LoadObject(innerObject, i + 1u));
                        }
                        else
                        {
                            propertyInfo.SetValue(obj, Convert.ChangeType(property.Value, propertyInfo.PropertyType));
                        }
                    }
                    foreach (XElement property in objectElement.Elements("field"))
                    {
                        XElement innerObject = property.Element("object");
                        FieldInfo fieldInfo = objectType.GetSaveableField(property.Attribute("name").Value, Settings.IncludeInheritedFields);
                        if (innerObject != null && Settings.CheckDepth(i))
                        {
                            fieldInfo.SetValue(obj, LoadObject(innerObject, i + 1u));
                        }
                        else
                        {
                            fieldInfo.SetValue(obj, Convert.ChangeType(property.Value, fieldInfo.FieldType));
                        }
                    }
                }
                return obj;
            }
        }
        protected virtual void LoadJson()
        {
            using (FileStream fileStream = File.OpenRead(Path))
            {
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    using (JsonReader jsonReader = new JsonTextReader(streamReader))
                    {
                        SaveField saveField = new SaveField(null, null);
                        while (jsonReader.Read())
                        {
                            string value = jsonReader.Value as string;
                            if (jsonReader.TokenType == JsonToken.PropertyName && saveField.Name == null)
                            {
                                saveField.Name = value;
                            }
                            if (jsonReader.TokenType == JsonToken.StartObject && saveField.Name != null)
                            {
                                saveField.SaveObject = LoadObject(0u);
                            }
                            if (jsonReader.TokenType == JsonToken.EndObject)
                            {
                                if (saveField.SaveObject != null)
                                {
                                    SaveFields.Add(saveField);
                                }
                                saveField = new SaveField(null, null);
                            }
                        }
                        object LoadObject(uint i)
                        {
                            string lastValue = null;
                            object currentObject = null;
                            Type currentObjectType = null;
                            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndObject)
                            {
                                string value = jsonReader.Value as string;
                                if (jsonReader.TokenType == JsonToken.StartObject)
                                {
                                    if (currentObjectType.IsList())
                                    {
                                        IList list = (IList)currentObject;
                                        list.Add(LoadObject(i + 1u));
                                    }
                                    else
                                    {
                                        if (lastValue[0] == '(' && lastValue[^1] == ')') // field
                                        {
                                            FieldInfo fieldInfo = currentObjectType.GetSaveableField(lastValue[1..^1], Settings.IncludeInheritedFields);
                                            fieldInfo.SetValue(currentObject, LoadObject(i + 1u));
                                        }
                                        else
                                        {
                                            PropertyInfo propertyInfo = currentObjectType.GetSaveableProperty(lastValue, Settings.IncludeInheritedProperties);
                                            propertyInfo.SetValue(currentObject, LoadObject(i + 1u));
                                        }
                                    }
                                }
                                else if (jsonReader.TokenType == JsonToken.String)
                                {
                                    if (lastValue == "type")
                                    {
                                        currentObjectType = Type.GetType(value);
                                        currentObject = Activator.CreateInstance(currentObjectType);
                                    }
                                    else
                                    {
                                        if (currentObjectType.IsList())
                                        {
                                            IList list = (IList)currentObject;
                                            list.Add(Convert.ChangeType(value, Type.GetType(lastValue.Split(' ')[2])));
                                        }
                                        else
                                        {
                                            if (lastValue[0] == '(' && lastValue[^1] == ')') // field
                                            {
                                                FieldInfo fieldInfo = currentObjectType.GetSaveableField(lastValue[1..^1], Settings.IncludeInheritedFields);
                                                fieldInfo.SetValue(currentObject, Convert.ChangeType(value, fieldInfo.FieldType));
                                            }
                                            else
                                            {
                                                PropertyInfo propertyInfo = currentObjectType.GetSaveableProperty(lastValue, Settings.IncludeInheritedProperties);
                                                propertyInfo.SetValue(currentObject, Convert.ChangeType(value, propertyInfo.PropertyType));
                                            }
                                        }
                                    }
                                }
                                lastValue = value;
                            }
                            return currentObject;
                        }
                    }
                }
            }
        }
        public static void QuickSave(string path, object obj, string name) => QuickSave(path, obj, name, new GameSaveSettings());
        public static void QuickSave(string path, object obj, string name, GameSaveSettings settings) => QuickSave(path, new SaveField(name, obj), settings);
        public static void QuickSave(string path, SaveField saveField) => QuickSave(path, saveField, new GameSaveSettings());
        public static void QuickSave(string path, SaveField saveField, GameSaveSettings settings)
        {
            GameSave gameSave = new GameSave(path, settings);
            gameSave.Modify(saveField.SaveObject, saveField.Name);
            gameSave.SaveChanges();
        }
        public static object QuickLoad(string path, string name) => QuickLoad(path, name, new GameSaveSettings());
        public static object QuickLoad(string path, string name, GameSaveSettings settings) => QuickLoad<object>(path, name, settings);
        public static T QuickLoad<T>(string path, string name) => QuickLoad<T>(path, name, new GameSaveSettings());
        public static T QuickLoad<T>(string path, string name, GameSaveSettings settings) => new GameSave(path, settings).GetObject<T>(name);
    }
    internal static class GameSaveExtensions
    {
        public static bool IsPrimitive(this PropertyInfo propertyInfo) => propertyInfo.PropertyType.IsPrimitiveOrStringOrDecimal();
        public static bool IsPrimitive(this FieldInfo fieldInfo) => fieldInfo.FieldType.IsPrimitiveOrStringOrDecimal();
        public static bool IsPrimitiveOrStringOrDecimal(this Type type) => type.IsPrimitive || type == typeof(decimal) || type == typeof(string);
        public static bool IsList(this Type type) => type.GetInterface(nameof(IList)) != null;
        public static IEnumerable<PropertyInfo> GetSaveableProperties(this Type objectType, bool includeInherited)
        {
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            if (!includeInherited)
            {
                bindingFlags |= BindingFlags.DeclaredOnly;
            }
            return objectType.GetProperties(bindingFlags).Where(pi => pi.GetMethod != null && pi.SetMethod != null && pi.GetCustomAttribute(typeof(ExcludeFromSaveAttribute)) == null);
        }
        public static IEnumerable<FieldInfo> GetSaveableFields(this Type objectType, bool includeInherited)
        {
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            if (!includeInherited)
            {
                bindingFlags |= BindingFlags.DeclaredOnly;
            }
            return objectType.GetFields(bindingFlags).Where(fi => !fi.IsInitOnly && fi.GetCustomAttribute(typeof(ExcludeFromSaveAttribute)) == null);
        }
        public static PropertyInfo GetSaveableProperty(this Type objectType, string saveName, bool includeInherited)
        {
            foreach (PropertyInfo propertyInfo in objectType.GetSaveableProperties(includeInherited))
            {
                if (propertyInfo.GetCustomAttribute<SaveNameAttribute>() is SaveNameAttribute saveNameAttribute && saveNameAttribute.Name == saveName)
                {
                    return propertyInfo;
                }
                if (propertyInfo.Name == saveName)
                {
                    return propertyInfo;
                }
            }
            throw new ArgumentException($"The type \"{objectType.FullName}\" does not have a property named \"{saveName}\".");
        }
        public static FieldInfo GetSaveableField(this Type objectType, string saveName, bool includeInherited)
        {
            foreach (FieldInfo fieldInfo in objectType.GetSaveableFields(includeInherited))
            {
                if (fieldInfo.GetCustomAttribute<SaveNameAttribute>() is SaveNameAttribute saveNameAttribute && saveNameAttribute.Name == saveName)
                {
                    return fieldInfo;
                }
                if (fieldInfo.Name == saveName)
                {
                    return fieldInfo;
                }
            }
            throw new ArgumentException($"The type \"{objectType.FullName}\" does not have a field named \"{saveName}\".");
        }
        public static string GetSaveableName(this PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetCustomAttribute<SaveNameAttribute>() is SaveNameAttribute saveName)
            {
                return saveName.Name;
            }
            return propertyInfo.Name;
        }
        public static string GetSaveableName(this FieldInfo fieldInfo)
        {
            if (fieldInfo.GetCustomAttribute<SaveNameAttribute>() is SaveNameAttribute saveName)
            {
                return saveName.Name;
            }
            return fieldInfo.Name;
        }
        public static bool CheckDepth(this GameSaveSettings settings, uint i) => settings.Depth > 0u && settings.Depth <= GameSaveSettings.MaxDepth ? settings.Depth > i : GameSaveSettings.MaxDepth > i;
    }
}