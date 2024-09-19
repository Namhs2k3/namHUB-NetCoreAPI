using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.USER
{
    [ApiController]
    [Route("api/vnpay")]
    public class VnPayIPNController : ControllerBase
    {
        private readonly ILogger<VnPayIPNController> _logger;
        private readonly IConfiguration _configuration;

        public VnPayIPNController(ILogger<VnPayIPNController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("ipn")]
        public IActionResult VnPayIPN()
        {
            _logger.LogInformation("Begin VNPAY IPN, URL={Url}", Request.Path + Request.QueryString);

            string returnContent = string.Empty;
            if (Request.Query.Count > 0)
            {
                string vnp_HashSecret = _configuration["VNPay:vnp_HashSecret"];
                var vnpayData = Request.Query;

                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (var param in vnpayData)
                {
                    if (!string.IsNullOrEmpty(param.Key) && param.Key.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(param.Key, param.Value);
                    }
                }

                long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
                long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
                long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                string vnp_SecureHash = vnpayData["vnp_SecureHash"];
                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    // Cập nhật kết quả giao dịch trong CSDL dựa trên orderId
                    Order order = new Order(); // Giả sử đã lấy ra OrderInfo từ DB

                    if (order != null)
                    {
                        if (order.TotalAmount == vnp_Amount)
                        {
                            if (order.Status == "0")
                            {
                                if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                                {
                                    _logger.LogInformation("Thanh toán thành công, OrderId={OrderId}, VNPAY TranId={TranId}", orderId, vnpayTranId);
                                    order.Status = "1"; // Đã thanh toán
                                }
                                else
                                {
                                    _logger.LogInformation("Thanh toán lỗi, OrderId={OrderId}, VNPAY TranId={TranId}, ResponseCode={ResponseCode}", orderId, vnpayTranId, vnp_ResponseCode);
                                    order.Status = "2"; // Giao dịch lỗi
                                }

                                // Cập nhật vào CSDL
                                returnContent = "{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}";
                            }
                            else
                            {
                                returnContent = "{\"RspCode\":\"02\",\"Message\":\"Order already confirmed\"}";
                            }
                        }
                        else
                        {
                            returnContent = "{\"RspCode\":\"04\",\"Message\":\"Invalid amount\"}";
                        }
                    }
                    else
                    {
                        returnContent = "{\"RspCode\":\"01\",\"Message\":\"Order not found\"}";
                    }
                }
                else
                {
                    _logger.LogInformation("Invalid signature, InputData={InputData}", Request.Path + Request.QueryString);
                    returnContent = "{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}";
                }
            }
            else
            {
                returnContent = "{\"RspCode\":\"99\",\"Message\":\"Input data required\"}";
            }

            return Content(returnContent, "application/json");
        }
    }
}
