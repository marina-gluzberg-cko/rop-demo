using Optional;

namespace Rop.Demo.WebApi.Domain
{
    public class ServiceResult<TResult, TError>
    {
        public Option<TResult> Result { get; }
        public Option<TError> Error { get; }
        public bool HasError => Error.HasValue;

        private ServiceResult(Option<TResult> result) 
        {
            Result = result;
            Error = Option.None<TError>();
        }

        private ServiceResult(Option<TError> error) 
        {
            Result = Option.None<TResult>();
            Error = error;
        }

        public static ServiceResult<TResult, TError> WithSuccessStatus(TResult result)
        {
            return new ServiceResult<TResult, TError>(Option.Some(result));
        }

        public static ServiceResult<TResult, TError> WithFailureStatus(TError errorResponse)
        {
            return new ServiceResult<TResult, TError>(Option.Some(errorResponse));
        }
    }
}
