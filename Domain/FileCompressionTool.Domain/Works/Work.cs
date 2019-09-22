using System;

namespace FileCompressionTool.Domain.Works
{
    public class Work : IWork, IDisposable
    {
        Action _work;

        public Work(Action work)
        {
            _work = work;
        }

        public void Run()
        {
            _work?.Invoke();
        }

        public void Dispose()
        {
            _work = null;
        }
    }
}