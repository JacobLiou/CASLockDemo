using CASLockDemo;
using System.Diagnostics;
using System.Linq;
// https://www.cnblogs.com/wei325/p/16065342.html

//无锁和有锁
// 　CAS在.NET中的实现类是Interlocked，内部提供很多原子操作的方法，最终都是调用Interlocked.CompareExchange(ref out,更新值，期望值) //基于内存屏障的方式操作
namespace CASLockDemo;

public class Program
{
    private static long _currentTime;
    private static long _current;
    private static Semaphore _semaphore = new Semaphore(0, 10);

    public static long FixedWindow()
    {

        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        var ct = Interlocked.Read(ref _currentTime);
        if (now > ct)
        {
            if (Interlocked.CompareExchange(ref _currentTime, now, ct) == ct)
            {
                Interlocked.Exchange(ref _current, 0);
            }
        }

        return Interlocked.Increment(ref _current);
    }

    /// <summary>
    /// 获取自增   这段代码为什么能够做到线程安全 结果正确?---编译器优化
    /// </summary>
    public static void GetIncrement()
    {
        long result = 0;
        Console.WriteLine("开始计算");
        //10个并发执行
        Parallel.For(0, 10, (i) =>
        {
            for (int j = 0; j < 10000; j++)
            {
                result++;
            }
        });

        Console.WriteLine("结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");
    }
    public static void GetIncrement1()
    {
        long result = 0;
        Console.WriteLine("开始计算");
        var tasks = new Task[10];
        for (int i = 0; i < 10; ++i)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10000; j++)
                {
                    result++;
                }
            });
        }

        Task.WaitAll(tasks);
        Console.WriteLine("结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");
    }

    public static void GetIncrement2()
    {
        long result = 0;
        Console.WriteLine("开始计算");
        var threads = new Thread[10];
        for (int i = 0; i < 10; ++i)
        {
            threads[i] = new Thread(() =>
             {
                 for (int j = 0; j < 10000; j++)
                 {
                     result++;
                 }
             });
        }

        // https://stackoverflow.com/questions/4190949/create-multiple-threads-and-wait-for-all-of-them-to-complete
        // threads.ForEach(thread => thread.Join());
        Array.ForEach(threads, thread => thread.Join());
        Console.WriteLine("结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");

        //C# threading task namepsaces ....
    }

    public static void GetIncrement3()
    {
        long result = 0;
        Console.WriteLine("开始计算");
        for (int i = 0; i < 10; ++i)
        {
            ThreadPool.QueueUserWorkItem((WaitCallback) =>
               {
                   for (int j = 0; j < 10000; j++)
                   {
                       result++;
                   }
               });
        }

        Console.WriteLine("结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");

    }


    private static Object _obj = new object();
    /// <summary>
    /// 原子操作基于Lock实现
    /// </summary>
    public static void AtomicityForLock()
    {

        long result = 0;
        Console.WriteLine("开始计算");
        //10个并发执行
        Parallel.For(0, 10, (i) =>
         {
             //lock锁
             lock (_obj)
             {
                 for (int j = 0; j < 10000; j++)
                 {
                     result++;
                 }
             }
         });
        Console.WriteLine("结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");

    }

    /// <summary>
    /// 自增CAS实现
    /// </summary>
    public static void AtomicityForInterLock()
    {
        long result = 0;
        Console.WriteLine("开始计算");
        Parallel.For(0, 10, (i) =>
         {
             for (int j = 0; j < 10000; j++)
             {
                 //自增
                 Interlocked.Increment(ref result);
             }
         });
        Console.WriteLine($"结束计算");
        Console.WriteLine($"result正确值应为：{10000 * 10}");
        Console.WriteLine($"result    现值为：{result}");
    }

    //创建自旋锁  Spinlock vs Mutex //
    private static SpinLock spin = new SpinLock();
    public static void Spinklock()
    {
        Action action = () =>
        {
            bool lockTaken = false;
            try
            {
                    //申请获取锁
                    spin.Enter(ref lockTaken);
                    //临界区
                    for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"当前线程{Thread.CurrentThread.ManagedThreadId.ToString()},输出:1");
                }
            }
            finally
            {
                    //工作完毕，或者产生异常时，检测一下当前线程是否占有锁，如果有了锁释放它
                    //避免出行死锁
                    if (lockTaken)
                {
                    spin.Exit();
                }
            }
        };
        Action action2 = () =>
        {
            bool lockTaken = false;
            try
            {
                    //申请获取锁
                    spin.Enter(ref lockTaken);
                    //临界区
                    for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"当前线程{Thread.CurrentThread.ManagedThreadId.ToString()},输出:2");
                }

            }
            finally
            {
                    //工作完毕，或者产生异常时，检测一下当前线程是否占有锁，如果有了锁释放它
                    //避免出行死锁
                    if (lockTaken)
                {
                    spin.Exit();
                }
            }

        };
        //并行执行2个action
        Parallel.Invoke(action, action2);

    }

    //读写锁， //策略支持递归
        private static ReaderWriterLockSlim rwl = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private static int index = 0;
        static void read()
        {
            try
            {
                //进入读锁
                rwl.EnterReadLock();
                for (int i = 0; i < 5; i++)
                {
                    Console.WriteLine($"线程id:{Thread.CurrentThread.ManagedThreadId},读数据,读到index:{index}");
                }
            }
            finally
            {
                //退出读锁
                rwl.ExitReadLock();
            }
        }
        static void write()
        {
            try
            {
                //尝试获写锁
                while (!rwl.TryEnterWriteLock(50))
                {
                    Console.WriteLine($"线程id:{Thread.CurrentThread.ManagedThreadId},等待写锁");
                }
                Console.WriteLine($"线程id:{Thread.CurrentThread.ManagedThreadId},获取到写锁");
                for (int i = 0; i < 5; i++)
                {
                    index++;
                    Thread.Sleep(50);
                }
                Console.WriteLine($"线程id:{Thread.CurrentThread.ManagedThreadId},写操作完成");
            }
            finally
            {
                //退出写锁
                rwl.ExitWriteLock();
            }
        }

        /// <summary>
        /// 执行多线程读写
        /// </summary>
        public static void test()
        {
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
            Task[] task = new Task[6];
            task[1] = taskFactory.StartNew(write); //写
            task[0] = taskFactory.StartNew(read); //读
            task[2] = taskFactory.StartNew(read); //读
            task[3] = taskFactory.StartNew(write); //写
            task[4] = taskFactory.StartNew(read); //读
            task[5] = taskFactory.StartNew(read); //读

            for (var i=0; i<6; i++)
            {
                task[i].Wait();
            }

        }

    static void Main(string[] args)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        GetIncrement();
        stopwatch.Stop();
        Console.WriteLine(stopwatch.ElapsedMilliseconds);
        // stopwatch.Restart();
        // AtomicityForLock();
        // stopwatch.Stop();
        // Console.WriteLine(stopwatch.ElapsedMilliseconds);

        Spinklock();
        Thread.Sleep(1000);

        Console.ReadKey();
        _currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        _current = 0;
        for (int i = 0; i < 10; i++)
        {
            Task.Factory.StartNew(() =>
            {
                for (int j = 0; j < 10000; j++)
                {
                    FixedWindow();
                }

                _semaphore.Release(1);
            });
        }
        //等待全部信号量释放
        for (int i = 0; i < 10; i++)
        {
            _semaphore.WaitOne();
        }

        // var tasks = new Task[10];
        // for (int i = 0; i < tasks.Length; ++i)
        // {
        //     var task = Task.Run(() =>
        //     {
        //         for (int j = 0; j < 10000; j++)
        //         {
        //             FixedWindow();
        //         }
        //     });
        //     tasks[i] = task;
        // }


        // Task.WhenAll(tasks).Wait();

        Console.WriteLine(_current);
        Console.WriteLine("sleep 2s");
        Thread.Sleep(2000);
        Console.WriteLine(FixedWindow());
    }


}