using System;
using System.Reflection;
using System.Text.Json.Nodes;

namespace XyGraph
{
    /// <summary>
    /// Definition of a graph-scoped variable (name + type). Runtime value lives in
    /// Graph.variableValues[Id] and is cleared at the start of each graph run.
    /// </summary>
    public class GraphVariable
    {
        public Guid   Id      { get; set; } = Guid.NewGuid();
        public string Name    { get; set; } = "variable";
        public string TypeAqn { get; set; } = typeof(object).AssemblyQualifiedName;

        public Type ResolvedType
        {
            get { return ResolveType(TypeAqn); }
            set { TypeAqn = value?.AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName; }
        }

        /// <summary>
        /// Resolves a type from an assembly-qualified name, full name, or simple name.
        /// Type.GetType alone only searches the calling assembly and the core library, so
        /// types defined by the host application (Kraken's models, for example) fail to
        /// resolve from inside XyGraph — hence the sweep over loaded assemblies.
        /// </summary>
        public static Type ResolveType(string nameOrAqn)
        {
            if (string.IsNullOrWhiteSpace(nameOrAqn)) return typeof(object);

            try
            {
                Type direct = Type.GetType(nameOrAqn, false, true);
                if (direct != null) return direct;
            }
            catch { }

            // strip any assembly qualification so a simple/full name comparison can match
            string bareName = nameOrAqn.Split(',')[0].Trim();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type byName;
                try { byName = asm.GetType(bareName, false, true); } catch { continue; }
                if (byName != null) return byName;
            }

            string simpleName = bareName.Contains('.') ? bareName.Substring(bareName.LastIndexOf('.') + 1) : bareName;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (Type t in types)
                    if (string.Equals(t.Name, simpleName, StringComparison.OrdinalIgnoreCase)) return t;
            }

            return typeof(object);
        }

        public JsonObject Save() => new JsonObject
        {
            ["id"]   = Id.ToString(),
            ["name"] = Name,
            ["type"] = TypeAqn
        };

        public static GraphVariable Load(JsonObject obj)
        {
            if (obj == null) return new GraphVariable();
            GraphVariable v = new GraphVariable();
            string idStr = obj["id"]?.GetValue<string>() ?? string.Empty;
            if (Guid.TryParse(idStr, out Guid id)) v.Id = id;
            v.Name   = obj["name"]?.GetValue<string>() ?? "variable";
            string t = obj["type"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(t)) v.TypeAqn = t;
            return v;
        }
    }
}
