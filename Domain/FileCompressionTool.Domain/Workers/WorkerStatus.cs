using System;

namespace FileCompressionTool.Domain.Workers
{
    [FlagsAttribute]
    public enum WorkerStatus
    {
        /// <summary>
        /// Рабочий поток создан
        /// </summary>
        Created,

        /// <summary>
        /// Рабочий поток выполняет работу
        /// </summary>
        Running,

        /// <summary>
        /// Рабочий поток ожидает работу
        /// </summary>
        WaitingForWork,

        /// <summary>
        /// Рабочий поток завершил выполнение работы с ошибкой
        /// </summary>
        Faulted,

        /// <summary>
        /// Рабочий поток успешно завершил выполнение работы
        /// </summary>
        Completed,

        /// <summary>
        /// Рабочий поток был отменен
        /// </summary>
        Canceled,

        /// <summary>
        /// Рабочий поток завершил выполнение всех работ и был остановлен
        /// </summary>
        Stopped
    }
}