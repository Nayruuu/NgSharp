using System;
using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp
{
    public class NgElement
    {
        public string Key { get; set; }
        
        public object Value { get; private set; }

        public NgElement Parent { get; private set; }

        public JsonValueKind ValueKind { get; private set; }

        public List<NgElement> Children { get; private set; } = new();

        public Dictionary<string, NgElement> Properties { get; private set; } = new();

        public string Path
        {
            get
            {
                if (Parent == null)
                    return Key;

                if (Key.StartsWith("["))
                    return $"{Parent.Path}{Key}";

                return string.IsNullOrEmpty(Parent.Path)
                    ? Key
                    : $"{Parent.Path}.{Key}";
            }
        }

        public string GetString()
        {
            return Value?.ToString();
        }

        public bool? GetBoolean()
        {
            if (Value is bool b)
                return b;

            if (Value is string s && bool.TryParse(s, out var result))
                return result;

            return null;
        }

        public DateTime? GetDateTime()
        {
            if (Value is DateTime dt)
                return dt;

            if (Value is string s && DateTime.TryParse(s, out var parsed))
                return parsed;

            return null;
        }

        public int? GetInt()
        {
            if (Value is int i) 
                return i;
            
            if (Value is long l) 
                return (int)l;
            
            if (Value is double d) 
                return (int)d;
            
            if (Value is float f) 
                return (int)f;
            
            if (Value is decimal dec) 
                return (int)dec;
            
            if (Value is string s && int.TryParse(s, out var result)) 
                return result;
            
            return null;
        }
        
        public long? GetLong()
        {
            if (Value is long l) 
                return l;
            
            if (Value is int i) 
                return i;
            
            if (Value is double d) 
                return (long)d;
            
            if (Value is float f) 
                return (long)f;
            
            if (Value is decimal dec) 
                return (long)dec;
            
            if (Value is string s && long.TryParse(s, out var result)) 
                return result;
            
            return null;
        }
        
        public float? GetFloat()
        {
            if (Value is float f) 
                return f;
            
            if (Value is double d) 
                return (float)d;
            
            if (Value is int i) 
                return i;
            
            if (Value is long l) 
                return l;
            
            if (Value is decimal dec) 
                return (float)dec;
            
            if (Value is string s && float.TryParse(s, out var result)) 
                return result;
            
            return null;
        }

        public decimal? GetDecimal()
        {
            if (Value is decimal d)
                return d;

            if (Value is int i)
                return i;

            if (Value is long l)
                return l;

            if (Value is double db)
                return (decimal)db;

            if (Value is string s && decimal.TryParse(s, out var parsed))
                return parsed;

            return null;
        }
        
        public double? GetDouble()
        {
            if (Value is double d) 
                return d;
            
            if (Value is float f) 
                return f;
            
            if (Value is int i)
                return i;
            
            if (Value is long l) 
                return l;
            
            if (Value is decimal dec) 
                return (double)dec;
            
            if (Value is string s && double.TryParse(s, out var result)) 
                return result;
            
            return null;
        }

        public static NgElement Parse(string literal)
        {
            object value;

            if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
                value = null;
            else if (string.Equals(literal, "true", StringComparison.OrdinalIgnoreCase))
                value = true;
            else if (string.Equals(literal, "false", StringComparison.OrdinalIgnoreCase))
                value = false;
            else if (int.TryParse(literal, out var i))
                value = i;
            else if (decimal.TryParse(literal, out var d))
                value = d;
            else
                value = literal;

            return new NgElement
            {
                Key = "",
                Value = value,
                ValueKind = value switch
                {
                    null => JsonValueKind.Null,
                    bool => (bool)value ? JsonValueKind.True : JsonValueKind.False,
                    int or long or decimal or double => JsonValueKind.Number,
                    _ => JsonValueKind.String
                }
            };
        }

        public NgElement SelectToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var segments = path
                .Replace("[", ".[").Split('.', StringSplitOptions.RemoveEmptyEntries);

            var current = this;

            foreach (var segment in segments)
            {
                if (segment.StartsWith("[") && segment.EndsWith("]"))
                {
                    if (!int.TryParse(segment.Trim('[', ']'), out int index))
                        return null;

                    if (current.Children == null || index < 0 || index >= current.Children.Count)
                        return null;

                    current = current.Children[index];
                }
                else
                {
                    if (current.Properties == null || !current.Properties.TryGetValue(segment, out var next))
                        return null;

                    current = next;
                }
            }

            return current;
        }
        
        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is not NgElement other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (ValueKind != other.ValueKind)
                return false;

            return ValueKind switch
            {
                JsonValueKind.String => GetString() == other.GetString(),
                JsonValueKind.Number => GetDouble() == other.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => GetBoolean() == other.GetBoolean(),
                JsonValueKind.Null => true,
                JsonValueKind.Object => Value.Equals(other.Children),
                JsonValueKind.Array => Value.Equals(other.Children),
                _ => Value?.Equals(other.Value) ?? other.Value == null
            };
        }

        public static NgElement FromJson(JsonElement jsonElement, NgElement parent = null, string key = "")
        {
            var ng = new NgElement
            {
                Key = key,
                Parent = parent,
                ValueKind = jsonElement.ValueKind,
                Properties = new(),
                Children = new(),
                Value = JsonElementToObject(jsonElement)
            };

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in jsonElement.EnumerateObject())
                    {
                        ng.Properties[prop.Name] = FromJson(prop.Value, ng, prop.Name);
                    }

                    break;

                case JsonValueKind.Array:
                    int i = 0;

                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        ng.Children.Add(FromJson(item, ng, $"[{i++}]"));
                    }

                    break;

                default:
                    ng.Value = JsonElementToObject(jsonElement);
                    break;
            }

            return ng;
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l
                    : element.TryGetDouble(out var d) ? d
                    : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText() // pour Object, Array, Undefined...
            };
        }
    }
}