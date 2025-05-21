using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace TestProject
{
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple C# Test Application with NuGet packages");
            Console.WriteLine("==============================================");

            // Test Newtonsoft.Json serialization
            var person = new Person 
            { 
                Name = "John Doe", 
                Age = 30, 
                Email = "john@example.com" 
            };

            string json = JsonConvert.SerializeObject(person, Formatting.Indented);
            Console.WriteLine("JSON Serialization:");
            Console.WriteLine(json);

            var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);
            Console.WriteLine($"\nDeserialized: {deserializedPerson?.Name}, Age: {deserializedPerson?.Age}");

            // Test HTTP client with dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();

            try
            {
                Console.WriteLine("\nTesting HTTP client...");
                var response = await httpClient.GetStringAsync("https://httpbin.org/json");
                var jsonResponse = JsonConvert.DeserializeObject(response);
                Console.WriteLine("HTTP Response received and parsed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
            }

            Console.WriteLine("\nApplication completed successfully!");
        }
    }
}
