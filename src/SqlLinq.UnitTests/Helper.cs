using System.Collections;
using System.Diagnostics;

namespace SqlLinq.UnitTests
{
    static class Helper
    {
        public static void Dump(IEnumerable a, IEnumerable b)
        {
            Debug.WriteLine("COLLECTION A");
            Dump(a);

            Debug.WriteLine("COLLECTION B");
            Dump(b);
        }

        public static void Dump(IEnumerable e)
        {
            Debug.Indent();
            foreach (var o in e)
            {
                if (o != null)
                    Debug.WriteLine(o.ToString());
                else
                    Debug.WriteLine("*NULL*");
            }
            Debug.Unindent();
        }
    }
}
