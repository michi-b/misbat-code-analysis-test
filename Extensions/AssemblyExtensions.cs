#region

using System.Reflection;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class AssemblyExtensions
{
    public static Assembly FindTransitivelyReferenced(this Assembly target, string name) => FindTransitivelyReferenced(target, a => a.GetName().Name == name);

    private static Assembly FindTransitivelyReferenced(this Assembly target, Predicate<Assembly> predicate)
    {
        var consumedAssemblies = new HashSet<string>();
        var assembliesToCheck = new Queue<Assembly>();

        assembliesToCheck.Enqueue(target);

        while (assembliesToCheck.Count > 0)
        {
            Assembly assemblyToCheck = assembliesToCheck.Dequeue();
            AssemblyName[] referencedAssemblies = assemblyToCheck.GetReferencedAssemblies();
            foreach (AssemblyName referencedAssembly in referencedAssemblies)
            {
                string assemblyName = referencedAssembly.FullName;
                Assembly assembly = Assembly.Load(referencedAssembly);

                if (predicate(assembly))
                {
                    return assembly;
                }

                if (!consumedAssemblies.Contains(assemblyName))
                {
                    consumedAssemblies.Add(assemblyName);
                    assembliesToCheck.Enqueue(assembly);
                }
            }
        }

        throw new ArgumentException($"Assembly with name '{target.GetName()}' does not transitively reference any assembly that matches the predicate.");
    }
}