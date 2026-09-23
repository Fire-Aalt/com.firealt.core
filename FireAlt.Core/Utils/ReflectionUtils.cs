using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Assemblies;

namespace FireAlt.Core.Utility
{
    [NoAutoStaticsCleanup]
    public static class ReflectionUtils
    {
        private static Assembly[] _allAssemblies;
        /// <summary> Gets all currently loaded assemblies. </summary>
        public static Assembly[] AllAssemblies => _allAssemblies ??= CurrentAssemblies.GetLoadedAssemblies().ToArray();
        /// <summary> Checks if an assembly references another assembly. </summary>
        /// <param name="assembly"> The assembly to check. </param>
        /// <param name="reference"> The reference to check if the assembly has. </param>
        /// <returns> True if referencing. </returns>
        public static bool IsAssemblyReferencingAssembly(this Assembly assembly, Assembly reference)
        {
            if (assembly == reference)
            {
                return true;
            }

            var referenceName = reference.GetName().Name;
            return assembly.GetReferencedAssemblies().Any(referenced => referenced.Name == referenceName);
        }

        /// <summary> Gets all assemblies that reference the provided assembly. </summary>
        /// <param name="reference"> The reference. </param>
        /// <returns> All assemblies that reference the provided assembly. </returns>
        public static IEnumerable<Assembly> GetAllAssemblyWithReference(Assembly reference)
        {
            return AllAssemblies.Where(a => IsAssemblyReferencingAssembly(a, reference));
        }
        
        // Finds a field by name on the type or any base type (including private fields).
        public static FieldInfo GetFieldInHierarchy(Type startType, string fieldName)
        {
            var t = startType;
            while (t != null)
            {
                // DeclaredOnly so we check the fields declared on this exact type.
                var fi = t.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null) return fi;
                t = t.BaseType;
            }
            return null;
        }
        
        public static Assembly GetAssemblyWithType<T>()
        {
            var assemblyName = typeof(T).Assembly.GetName().Name;
            var assembly = AllAssemblies.First(a => a.GetName().Name == assemblyName);

            return assembly;
        }
        
        public static MethodInfo GetCallMethod(object targetObject, string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) 
                throw new Exception($"Method name is null or empty");
            return GetMethodSignature(targetObject, methodName);
        }

        public static FieldInfo GetField(object targetObject, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) 
                throw new Exception($"Field name is null or empty");
            return GetFieldSignature(targetObject, fieldName);
        }
        
        public static bool TryGetCallMethod(object targetObject, string methodName, out MethodInfo method)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                method = null;
                return false;
            }
            
            method = GetMethodSignature(targetObject, methodName);
            return true;
        }
        
        private static MethodInfo GetMethodSignature(object targetObject, string methodName)
        {
            var method = targetObject.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null) 
                throw new Exception($"Method '{methodName}' not found on target object '{targetObject}'.");
            return method;
        }
        
        private static FieldInfo GetFieldSignature(object targetObject, string fieldName)
        {
            var field = targetObject.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null) 
                throw new Exception($"Field '{fieldName}' not found on target object '{targetObject}'.");
            return field;
        }
    }
}
