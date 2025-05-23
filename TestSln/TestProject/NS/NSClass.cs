using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.NS
{
    public class NSClass
    {
        public TestClass TestClass { get; set; } = new TestClass();
        public NSClass() { }

        public async Task FuncAsync()
        {
            Person person = new Person();
            TestClass.person = person;
        }
    }
}
