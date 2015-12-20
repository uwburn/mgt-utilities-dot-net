using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.EventHandlers
{
    public delegate void GenericEventHandler(object sender);

    public delegate void GenericEventHandler<T>(object sender, T arg);

    public delegate void GenericEventHandler<T, U>(object sender, T arg1, U arg2);

    public delegate void GenericEventHandler<T, U, V>(object sender, T arg1, U arg2, V arg3);
}
