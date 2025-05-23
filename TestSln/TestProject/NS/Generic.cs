using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.NS
{
    public class Generic<T>
        where T : new()
    {
        public T Value { get; set; } = new();
    }
}
