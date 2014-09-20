using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ATC
{
    public static class Eviscerator
    {
        public static void Eviscerate(this object obj) 
        {
            Eviscerate(obj, 1);
        }

        public static void Eviscerate(this object obj, int depth)
        {
            Debug.Log(Eviscerate(obj, depth, 1));
        }

        private static string Eviscerate(object obj, int depth, int currentDepth) 
        {
            string indent = Indent(currentDepth);
            string outputString = "";
            if (obj == null)
            {
                outputString += "null";
                return outputString;
            }
            outputString += "{\n";

            //outputString += "=================================Eviscerating an object=================================\n";
            Type objType = obj.GetType();
            outputString += indent + "Object type: " + objType.Name + "\n";

            IEnumerable<PropertyInfo> propInfo = objType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (PropertyInfo prop in propInfo)
            {
                outputString += indent + "Property {\n";
                outputString += indent + "  Name: " + prop.Name + "\n";
                outputString += indent + "  Public: " + prop.CanRead + "\n";
                outputString += indent + "  Type: " + prop.PropertyType.Name + "\n";
                outputString += indent + "  Writable: " + prop.CanWrite + "\n";
                if (prop.CanRead)
                {
                    if (currentDepth < depth && prop.CanRead && !prop.PropertyType.IsValueType() && !prop.PropertyType.IsEnumerable())
                    {
                        //outputString += Eviscerate(prop.GetValue(obj, null), depth, currentDepth + 1);
                        outputString += indent + "  Value: ";
                        try
                        {
                            outputString += Eviscerate(prop.GetValue(obj, prop.GetIndexParameters().Count() > 0 ? new object[0] : null), depth, currentDepth + 1);
                        }
                        catch (Exception e)
                        {
                            outputString += e.Message;
                        }
                        finally
                        {
                            outputString += "\n";
                        }
                    }
                    else
                    {
                        outputString += indent + "  Value: ";
                        try
                        {
                            outputString += (prop.GetValue(obj, prop.GetIndexParameters().Count() > 0 ? new object[0] : null) ?? "null");
                        }
                        catch (Exception e)
                        {
                            outputString += e.Message;
                        }
                        finally
                        {
                            outputString += "\n";
                        }
                    }
                }
                outputString += indent + "}\n";
            }


            IEnumerable<FieldInfo> fieldInfo = objType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (FieldInfo field in fieldInfo)
            {
                outputString += indent + "Field {\n";
                outputString += indent + "  Name: " + field.Name + "\n";
                outputString += indent + "  Public: " + field.IsPublic + "\n";
                outputString += indent + "  Type: " + field.FieldType.Name + "\n";
                outputString += indent + "  Writable: " + !field.IsInitOnly + "\n";
                if (currentDepth < depth && !field.FieldType.IsValueType() && !field.FieldType.IsEnumerable())
                {
                    outputString += indent + "  Value: " + Eviscerate(field.GetValue(obj), depth, currentDepth + 1) + "\n";
                }
                else
                {
                    outputString += indent + "  Value: " + (field.GetValue(obj) ?? "null") + "\n";
                }
                outputString += indent + "}\n";
            }

            IEnumerable<MethodInfo> methodInfo = objType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"));

            foreach (MethodInfo method in methodInfo)
            {
                outputString += indent + "Method {\n";
                outputString += indent + "  Name: " + method.Name + "\n";
                outputString += indent + "  Public: " + method.IsPublic + "\n";
                outputString += indent + "  Parameters: (" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray()) + ")\n";
                outputString += indent + "  Return Type: " + method.ReturnType.Name + "\n";
                outputString += indent + "}\n";
            }

            outputString += "\n}";

            return outputString;
        }
        private static string Indent(int num)
        {
            string retVal = "";
            for (int c = 0; c < num; c++) retVal += "    ";
            return retVal;
        }

        private static bool IsEnumerable(this Type type)
        {
            return type.GetInterface("IEnumerable") != null;
        }

        private static bool IsValueType(this Type type)
        {
            return type.IsEnum || 
                   type == typeof(string) ||
                   type == typeof(short) ||
                   type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal) ||
                   type == typeof(char);
        }

        private static bool IsValueType(this object obj)
        {
            return obj is Enum || 
                   obj is string ||
                   obj is short ||
                   obj is int ||
                   obj is long ||
                   obj is bool ||
                   obj is byte ||
                   obj is float ||
                   obj is double ||
                   obj is decimal ||
                   obj is char;
        }

    }
}
