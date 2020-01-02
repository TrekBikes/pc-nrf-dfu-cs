using Nordic.nRF.DFU;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU.Util
{
    public static class DfuExceptionExtensions
    {
        public static async Task AwaitAndCheckException(this DfuAbstractTransport transport, Task awaitable)
        {
            // make this visible if there's already an exception there
            CheckExceptionWithThrow(transport);

            try 
            {
                // now, see what happens when the calling function executes...
                await awaitable;
            }
            catch (DfuException ex)
            {
                throw new DfuException(ex, transport.LastException);
            }

            // Check to see if anything bad happened outside the 'scope' of the await()
            CheckExceptionWithThrow(transport);
        }

        public static async Task<T> AwaitAndCheckException<T>(this DfuAbstractTransport transport, Task<T> awaitable)
        {
            // make this visible if there's already an exception there
            CheckExceptionWithThrow(transport);

            T operationResult = default(T);
            try 
            {
                // now, see what happens when the calling function executes...
                operationResult = await awaitable;
            }
            catch (DfuException ex)
            {
                throw new DfuException(ex, transport.LastException);
            }

            // Check to see if anything bad happened outside the 'scope' of the await()
            CheckExceptionWithThrow(transport);

            return operationResult;
        }

        private static void CheckExceptionWithThrow(DfuAbstractTransport transport)
        {
            var localException = transport.LastException;
            transport.LastException = null;

            if (localException != null)
            {
                throw localException;
            }
        }
    }
}