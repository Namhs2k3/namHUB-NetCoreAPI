using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace namHub_FastFood.Controller.EMPLOYEE
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public class EmployeesController : ControllerBase
    {
    }
}
