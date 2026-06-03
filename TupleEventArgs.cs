using System;

namespace MySQLConfigurationAndSsh;

public static class TupleEventArgs
{
    public static TupleEventArgs<T1> Create<T1>(T1 item1)
    {
        return new TupleEventArgs<T1>(item1);
    }

    public static TupleEventArgs<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
    {
        return new TupleEventArgs<T1, T2>(item1, item2);
    }

    public static TupleEventArgs<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
    {
        return new TupleEventArgs<T1, T2, T3>(item1, item2, item3);
    }
}

public class TupleEventArgs<T1> : EventArgs
{
    public T1 Item1;

    public TupleEventArgs(T1 item1)
    {
        Item1 = item1;
    }
}

public class TupleEventArgs<T1, T2> : EventArgs
{
    public T1 Item1;
    public T2 Item2;

    public TupleEventArgs(T1 item1, T2 item2)
    {
        Item1 = item1;
        Item2 = item2;
    }
}

public class TupleEventArgs<T1, T2, T3> : EventArgs
{
    public T1 Item1;
    public T2 Item2;
    public T3 Item3;

    public TupleEventArgs(T1 item1, T2 item2, T3 item3)
    {
        Item1 = item1;
        Item2 = item2;
        Item3 = item3;
    }
}