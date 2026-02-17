using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace GrindCore.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            int result = 0;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.WriteLine($"Unhandled exception: {e.ExceptionObject}");
                result = 1;
                Environment.Exit(result);
            };

            try
            {
                // Reference the assembly containing the test classes
                Assembly assembly = typeof(HashTests).Assembly;

                // Get all types in the assembly
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // Find methods marked with [Theory]
                    var theoryMethods = type.GetMethods()
                        .Where(m => m.GetCustomAttributesData()
                            .Any(ad => ad.AttributeType.Name == "TheoryAttribute" || ad.AttributeType.FullName == "Xunit.TheoryAttribute"))
                        .ToArray();

                    if (!theoryMethods.Any())
                        continue;

                    Console.WriteLine($"Class: {type.Name}");

                    foreach (var method in theoryMethods)
                    {
                        var inlineAttributeDatas = method.GetCustomAttributesData()
                            .Where(ad => ad.AttributeType.Name == "InlineDataAttribute" || ad.AttributeType.FullName == "Xunit.InlineDataAttribute")
                            .ToList();

                        foreach (var cad in inlineAttributeDatas)
                        {
                            // Convert constructor arguments to parameter array
                            object?[] parameters = cad.ConstructorArguments.Select(a => convertArgStatic(a)).ToArray();

                            // InlineDataAttribute commonly stores a single object[] as the ctor argument; unwrap if necessary
                            if (parameters.Length == 1 && parameters[0] is object[] inner)
                                parameters = inner;

                            object? testClassInstance = null;
                            try
                            {
                                // xUnit instantiates a new instance per test for non-static methods
                                if (!method.IsStatic)
                                {
                                    testClassInstance = Activator.CreateInstance(type);
                                    if (testClassInstance == null)
                                    {
                                        result = 1;
                                        Console.WriteLine($"Fail: {type.Name} could not be constructed");
                                        continue;
                                    }
                                }

                                // Invoke the test method
                                object? ret = method.Invoke(testClassInstance, parameters);

                                // If method returned a Task, wait synchronously
                                if (ret is Task t)
                                    t.GetAwaiter().GetResult();

                                Console.WriteLine($"Pass: {method.Name}({string.Join(", ", parameters)})");
                            }
                            catch (TargetInvocationException tie)
                            {
                                // Unwrap and report inner exception
                                Console.WriteLine($"Fail: {method.Name}({string.Join(", ", parameters)})");
                                result = 1;
                                printExceptionDetails(tie.InnerException ?? tie);
                                return result;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Fail: {method.Name}({string.Join(", ", parameters)})");
                                result = 1;
                                printExceptionDetails(ex);
                                return result;
                            }
                            finally
                            {
                                // Dispose instance if necessary and release references
                                if (testClassInstance is IDisposable d)
                                    d.Dispose();
                                testClassInstance = null;

                                // Perform a single GC pass to encourage prompt cleanup. Repeated aggressive
                                // collections can cause instability on some platforms; keep this minimal.
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                GC.Collect();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                result = 1;
            }

            return result;
        }

        static void printExceptionDetails(Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner Exception:");
                printExceptionDetails(ex.InnerException);
            }
        }

        // Helper moved out of local scope to avoid local-function capture issues
        private static object? convertArgStatic(CustomAttributeTypedArgument a)
        {
            if (a.Value == null)
                return null;
            if (a.Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> rc)
            {
                var arr = new object[rc.Count];
                for (int i = 0; i < rc.Count; i++)
                    arr[i] = convertArgStatic(rc[i])!;
                return arr;
            }
            return a.Value;
        }
    }
}
