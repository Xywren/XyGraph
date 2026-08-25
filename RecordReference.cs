using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XyGraph
{
    /// <summary>
    /// Marks the field or property that identifies a database record — the value a graph stores
    /// and reloads by. The type must have a constructor taking that value as its first argument;
    /// any further arguments must be optional.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DBIdentifierAttribute : Attribute { }

    /// <summary>
    /// Reads and writes the values a graph carries. A record is stored as its type and
    /// identifier, and read back out of the database, so a node always works from the row as it
    /// stands rather than a copy of how it looked when the value was produced. Anything without
    /// a [DBIdentifier] is stored as-is.
    /// </summary>
    public static class RecordReference
    {
        private const string TYPE_KEY = "recordType";
        private const string ID_KEY   = "recordId";

        // Reflection per value would be wasteful — every node input resolves through here.
        private static readonly Dictionary<Type, MemberInfo>      identifiers  = new Dictionary<Type, MemberInfo>();
        private static readonly Dictionary<Type, ConstructorInfo> constructors = new Dictionary<Type, ConstructorInfo>();

        public static JsonNode Write(object value)
        {
            object identifier = IdentifierOf(value);
            if (identifier == null) return JsonSerializer.SerializeToNode(value);

            return new JsonObject
            {
                [TYPE_KEY] = value.GetType().AssemblyQualifiedName,
                [ID_KEY]   = JsonSerializer.SerializeToNode(identifier)
            };
        }

        public static object Read(JsonNode node, Type expectedType)
        {
            if (node == null) return null;

            if (node is JsonObject stored && stored[TYPE_KEY] != null)
            {
                Type recordType = Type.GetType(stored[TYPE_KEY].GetValue<string>() ?? string.Empty);
                if (recordType == null) return null;

                MemberInfo identifier = IdentifierMember(recordType);
                if (identifier == null) return null;

                object key = stored[ID_KEY].Deserialize(IdentifierType(identifier));
                return Build(recordType, key);
            }

            if (expectedType == null) return null;

            try { return JsonSerializer.Deserialize(node.ToJsonString(), expectedType); }
            catch { return null; }
        }

        /// <summary>
        /// Returns the record as the database has it now. A port carries a reference to a row,
        /// not a copy of it, so a node reading one days after it was produced still sees current
        /// data. Anything that is not an identified record is passed straight back.
        /// </summary>
        public static object Reload(object value)
        {
            object identifier = IdentifierOf(value);
            if (identifier == null) return value;

            return Build(value.GetType(), identifier) ?? value;
        }

        private static object Build(Type recordType, object key)
        {
            if (key == null) return null;

            ConstructorInfo constructor = ConstructorFor(recordType, key.GetType());
            if (constructor == null) return null;

            ParameterInfo[] parameters = constructor.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = key;
            for (int i = 1; i < parameters.Length; i++) arguments[i] = parameters[i].DefaultValue;

            // A record deleted since the value was produced loads as null, so the node that
            // needs it fails its own null check and routes the process to Error.
            try { return constructor.Invoke(arguments); }
            catch { return null; }
        }

        /// <summary>The value of a record's [DBIdentifier], or null when it has none or is unsaved.</summary>
        private static object IdentifierOf(object value)
        {
            if (value == null) return null;

            MemberInfo member = IdentifierMember(value.GetType());
            if (member == null) return null;

            object identifier = member is FieldInfo field
                ? field.GetValue(value)
                : ((PropertyInfo)member).GetValue(value);

            // An unsaved record has nothing to reload from.
            if (identifier == null) return null;
            if (identifier is int number && number <= 0) return null;
            if (identifier is string text && string.IsNullOrEmpty(text)) return null;

            return identifier;
        }

        private static MemberInfo IdentifierMember(Type type)
        {
            if (identifiers.TryGetValue(type, out MemberInfo cached)) return cached;

            MemberInfo found = null;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

            foreach (FieldInfo field in type.GetFields(flags))
                if (field.GetCustomAttribute<DBIdentifierAttribute>() != null) found = field;

            if (found == null)
                foreach (PropertyInfo property in type.GetProperties(flags))
                    if (property.GetCustomAttribute<DBIdentifierAttribute>() != null) found = property;

            identifiers[type] = found;
            return found;
        }

        private static Type IdentifierType(MemberInfo member)
        {
            Type type = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static ConstructorInfo ConstructorFor(Type recordType, Type keyType)
        {
            if (constructors.TryGetValue(recordType, out ConstructorInfo cached)) return cached;

            ConstructorInfo found = null;
            foreach (ConstructorInfo candidate in recordType.GetConstructors())
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == 0) continue;
                if (parameters[0].ParameterType != keyType) continue;

                bool remainderOptional = true;
                for (int i = 1; i < parameters.Length; i++)
                    if (!parameters[i].IsOptional) remainderOptional = false;

                if (remainderOptional) found = candidate;
            }

            constructors[recordType] = found;
            return found;
        }
    }
}
