namespace RestaurantManagement.Api.Utils.Exceptions
{
    // Base class for known business/validation errors
    public class BusinessException : Exception
    {
        public int StatusCode { get; }

        public BusinessException(string message, int statusCode = 400)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}