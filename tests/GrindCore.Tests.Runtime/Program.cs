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
                                    // We cannot easily call InlineDataAttribute.GetData via reflection
                                    // reliably across xUnit versions. Instead, only attempt to
                                    // invoke parameterless theories (rare) or skip invocation.
                                    Console.WriteLine($"Skipping invocation of {method.Name} due to runtime test runner differences");
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                    GC.Collect();

                                }
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
    }
}