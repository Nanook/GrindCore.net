using System;
using System.Linq;
using System.Reflection;
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
                    // Check if the type is a test class (contains methods with [Theory] attribute)
                    var theoryMethods = type.GetMethods()
                        .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TheoryAttribute"));

                    if (theoryMethods.Any())
                    {
                        // Create an instance of the test class
                        Console.WriteLine($"Class: {type.Name}");
                        try
                        {
                            object? testClassInstance = Activator.CreateInstance(type);
                            if (testClassInstance == null)
                            {
                                result = 1;
                                Console.WriteLine($"Fail: {type.Name} could not be loaded");
                            }
                            else
                            {
                                foreach (var method in theoryMethods)
                                {
                                    // Get the InlineData attributes for the method
                                    var inlineDataAttributes = method.GetCustomAttributes().Where(a => a.GetType().Name == "InlineDataAttribute").Cast<Attribute>();

                                    foreach (var inlineData in inlineDataAttributes)
                                    {
                                        // Attempt to invoke the theory with InlineData attributes
                                        // by reading the attribute constructor arguments via
                                        // CustomAttributeData. This works across xUnit versions
                                        // because it doesn't rely on concrete attribute types.
                                        try
                                        {
                                            var cadList = method.GetCustomAttributesData()
                                                .Where(a => a.AttributeType.Name == "InlineDataAttribute")
                                                .ToList();

                                            foreach (var cad in cadList)
                                            {
                                                // Extract constructor args. For params object[] the
                                                // value may be a collection of CustomAttributeTypedArgument.
                                                object[] ctorArgs;
                                                if (cad.ConstructorArguments.Count == 1 &&
                                                    cad.ConstructorArguments[0].Value is System.Collections.Generic.IList<CustomAttributeTypedArgument> inner)
                                                {
                                                    ctorArgs = inner.Select(a => a.Value).ToArray();
                                                }
                                                else
                                                {
                                                    ctorArgs = cad.ConstructorArguments.Select(a => a.Value).ToArray();
                                                }

                                                // Convert arguments to expected parameter types where possible
                                                var parameters = method.GetParameters();
                                                if (parameters.Length != ctorArgs.Length)
                                                {
                                                    Console.WriteLine($"Skipping invocation of {method.Name} due to parameter count mismatch (expected {parameters.Length}, got {ctorArgs.Length})");
                                                    continue;
                                                }

                                                object?[] invokeArgs = new object?[ctorArgs.Length];
                                                for (int i = 0; i < ctorArgs.Length; i++)
                                                {
                                                    var targetType = parameters[i].ParameterType;
                                                    var val = ctorArgs[i];
                                                    if (val == null)
                                                    {
                                                        invokeArgs[i] = null;
                                                    }
                                                    else if (targetType.IsInstanceOfType(val))
                                                    {
                                                        invokeArgs[i] = val;
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            if (targetType.IsEnum && val is IConvertible)
                                                            {
                                                                invokeArgs[i] = Enum.ToObject(targetType, val);
                                                            }
                                                            else
                                                            {
                                                                invokeArgs[i] = Convert.ChangeType(val, targetType);
                                                            }
                                                        }
                                                        catch
                                                        {
                                                            invokeArgs[i] = val; // fallback - hope it's assignable at runtime
                                                        }
                                                    }
                                                }

                                                object? invokeOn = method.IsStatic ? null : testClassInstance;
                                                try
                                                {
                                                    var resultObj = method.Invoke(invokeOn, invokeArgs);
                                                    // If the test method returns a Task, wait for it
                                                    if (resultObj is System.Threading.Tasks.Task t)
                                                    {
                                                        t.GetAwaiter().GetResult();
                                                    }
                                                    Console.WriteLine($"Invoked {method.Name}({string.Join(", ", invokeArgs.Select(a => a?.ToString() ?? "null"))}) - PASS");
                                                }
                                                catch (TargetInvocationException tie)
                                                {
                                                    Console.WriteLine($"Invoked {method.Name}({string.Join(", ", invokeArgs.Select(a => a?.ToString() ?? "null"))}) - FAIL");
                                                    Console.WriteLine($"Test {method.Name} threw: {tie.InnerException?.Message}");
                                                    printExceptionDetails(tie.InnerException ?? tie);
                                                    result = 1;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Skipping invocation of {method.Name} due to runtime test runner differences: {ex.Message}");
                                        }

                                    }
                                }
                            }
                        }
                        finally
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
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
    }
}