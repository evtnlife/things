using System;
using System.Threading;

namespace MABIS.Services.Dispatch.Whatsapp.WindowsService.Common
{
    public class ThreadCustom
    {
        // Membros
        private Thread m_Thread;
        private DlgThreadTask m_ThreadTaskMethod;
        private string m_ThreadName;
        private int m_TriggerRate;
        private bool m_TerminateOrder = false;
        private ManualResetEvent m_TerminateEvent = new ManualResetEvent(false);
        private bool m_Terminated = false;
        private object m_UserData = null;
        private int m_ThreadID = -1;
        private EventWaitHandle m_TaskDelayHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private bool m_Reset = false;
        private bool m_Pause = false;
        private bool m_LogTerminate = false;

        // Constantes
        private int TERMINATE_TIME_OUT = 10000;
        private string v;
        private DlgThreadTask dlgThreadTask;

        // Delegate
        public delegate void DlgThreadTask(ref bool terminateOrder, object userData);

        /// <summary>
        /// Construtor padrão
        /// </summary>
        /// <param name="threadName">Nome da thread</param>
        /// <param name="threadTaskMethod">Método a ser chamado</param>
        /// <param name="triggerRate">Frequencia de disparo em milisegundos (0 = sem delay) (-1 = trigger manual) (menor que -1 = aguardo antecipado)</param>
        public ThreadCustom(string threadName, DlgThreadTask threadTaskMethod, int triggerRate) : this(threadName, threadTaskMethod, triggerRate, null)
        {
        }

        /// <summary>
        /// Construtor padrão
        /// </summary>
        /// <param name="threadName">Nome da thread</param>
        /// <param name="threadTaskMethod">Método a ser chamado</param>
        /// <param name="triggerRate">Frequencia de disparo em milisegundos (0 = sem delay) (-1 = trigger manual) (menor que -1 = aguardo antecipado)</param>
        /// <param name="userData">Dados do usuário</param>
        public ThreadCustom(string threadName, DlgThreadTask threadTaskMethod, int triggerRate, object userData)
        {
            m_ThreadName = threadName;
            m_ThreadTaskMethod = threadTaskMethod;
            m_TriggerRate = System.Runtime.GCSettings.IsServerGC && triggerRate == 0 ?
                5 :
                triggerRate;
            m_UserData = userData;
            m_Thread = new Thread(new ThreadStart(ThreadTask));
            m_Thread.Name = m_ThreadName;
            m_Thread.Start();
        }

        public ThreadCustom(string v, DlgThreadTask dlgThreadTask)
        {
            this.v = v;
            this.dlgThreadTask = dlgThreadTask;
        }

        /// <summary>
        /// Termina execução da thread
        /// </summary>
        public void Terminate()
        {
            Terminate(false, TERMINATE_TIME_OUT);
        }

        /// <summary>
        /// Termina execução da thread
        /// </summary>
        public void Terminate(bool asyncTerminate)
        {
            Terminate(asyncTerminate, TERMINATE_TIME_OUT);
        }

        /// <summary>
        /// Termina execução da thread
        /// </summary>
        public void Terminate(int terminateTimeOut)
        {
            Terminate(false, terminateTimeOut);
        }

        /// <summary>
        /// Tenta terminar a thread dentro de um tempo determinado (não executa o fechamento forçado)
        /// </summary>
        public bool TryTerminate(int terminateTimeOut)
        {
            if (!m_Terminated)
            {
                m_LogTerminate = true;
                Console.WriteLine(string.Format("TryTerminate da thread {0} com timeout {1} iniciado...", m_ThreadName, terminateTimeOut));
                m_TerminateOrder = true;
                if (m_TerminateEvent.WaitOne(terminateTimeOut, false))
                    return true;
                else
                    return false;
            }
            else
                return true;
        }

        /// <summary>
        /// Termina execução da thread
        /// </summary>
        private void Terminate(bool asyncTerminate, int terminateTimeOut)
        {
            m_LogTerminate = true;
            Console.WriteLine(string.Format("Terminate da thread {0} com timeout {1}, async {2}, terminated {3} e terminateorder {4} iniciado...",
                m_ThreadName,
                terminateTimeOut,
                asyncTerminate,
                m_Terminated,
                m_TerminateOrder));
            if (!m_Terminated)
            {
                m_TerminateOrder = true;
                if (!asyncTerminate)
                {
                    ManualTrigger();
                    if (!m_TerminateEvent.WaitOne(terminateTimeOut, false))
                    {
                        Console.WriteLine("Término de thread forçado. Nome da thread: " + m_ThreadName);
                        try { m_Thread.Abort(); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>
        /// Aguarda término da thread
        /// </summary>
        public void TerminateWaiting()
        {
            TerminateWaiting(-1);
        }

        /// <summary>
        /// Aguarda término "bunitinho" da thread dentro do timeout, senão parte para
        /// ignorância
        /// </summary>
        /// <param name="timeout">Timeout em ms (-1 aguarda indefinidamente)</param>
        public void TerminateWaiting(int timeout)
        {
            m_LogTerminate = true;
            Console.WriteLine(string.Format("TerminateWaiting da thread {0} com timeout {1} iniciado...", m_ThreadName, timeout));
            if (!m_TerminateEvent.WaitOne(timeout, false))
                Terminate(timeout);
        }

        /// <summary>
        /// Identificação da thread
        /// </summary>
        /// <returns></returns>
        public int ThreadID
        {
            get { return m_ThreadID; }
        }

        /// <summary>
        /// Disparo manual adiantado da thread
        /// </summary>
        public void ManualTrigger()
        {
            m_TaskDelayHandle.Set();
        }

        /// <summary>
        /// Tempo de disparo
        /// </summary>
        public int TriggerRate
        {
            get { return m_TriggerRate; }
            set { m_TriggerRate = value; }
        }

        /// <summary>
        /// Status do reset
        /// </summary>
        public bool ResetStatus
        {
            get
            {
                if (m_TaskDelayHandle.WaitOne(1, false))
                {
                    m_TaskDelayHandle.Set();
                    return true;
                }
                else
                    return false;
            }
        }

        /// <summary>
        /// Reinicia a contagem do tempo
        /// </summary>
        public void ResetTimer()
        {
            if (m_TriggerRate < -1)
                m_TaskDelayHandle.Set();
        }

        /// <summary>
        /// Pausa thread (somente quando TriggerRate for menor que -1)
        /// </summary>
        public void Pause()
        {
            if (m_TriggerRate < -1)
                m_Pause = true;
        }

        /// <summary>
        /// Método de execução da thread
        /// </summary>
        private void ThreadTask()
        {
            m_ThreadID = Thread.CurrentThread.ManagedThreadId;

            while (!m_TerminateOrder)
            {
                if (m_TriggerRate <= -1)
                {
                    if (m_TriggerRate == -1)
                        m_TaskDelayHandle.WaitOne();
                    else
                    {
                        m_Reset = true;
                        while (m_Reset)
                        {
                            m_Reset = false;
                            if (m_TaskDelayHandle.WaitOne(-1 * m_TriggerRate, false))
                                if (!m_TerminateOrder)
                                    m_Reset = true;
                        }
                    }
                    if (m_TerminateOrder) continue;
                }

                if (m_ThreadTaskMethod != null)
                    try
                    {
                        m_ThreadTaskMethod(ref m_TerminateOrder, m_UserData);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(string.Format("Exceção na thread: {0}", m_ThreadName));
                    }

                if (m_TriggerRate > 0)
                    m_TaskDelayHandle.WaitOne(m_TriggerRate, false);
                else
                    if (m_TriggerRate < -1)
                    if (m_Pause)
                    {
                        m_Pause = false;
                        m_TaskDelayHandle.WaitOne();
                    }
            }

            m_TerminateEvent.Set();
            m_Terminated = true;
            if (m_LogTerminate)
                Console.WriteLine(string.Format("Thread {0} terminada.", m_ThreadName));
        }
    }
}
