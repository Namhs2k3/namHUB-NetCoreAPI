using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/customer-manage-for-admin")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class CustomersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        public CustomersManageController(namHUBDbContext context)
        {
            _context = context;
        }

        [HttpGet("get-customer-list")]
        public async Task<IActionResult> GetCL()
        {
            return Ok();
        }

        
    }
}
