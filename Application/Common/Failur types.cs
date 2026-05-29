using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Common
{
  
         public enum ErrorType
        {
            None = 0,
            BadRequest = 400,
        Unauthorized = 401,
        NotFound = 404,
            Failure = 500, // General Internal Error
           Conflict = 409
        
        }
    public record Error(string Message, ErrorType Type);
}
