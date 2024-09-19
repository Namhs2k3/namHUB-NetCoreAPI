using System;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

public class VnPayService
{
    private readonly IConfiguration _configuration;

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(long orderId, decimal amount, string orderInfo, string returnUrl, string ipAddress, string locale = "vn", string bankCode = null)
    {
        string vnp_Returnurl = _configuration["VNPay:vnp_Returnurl"];
        string vnp_Url = _configuration["VNPay:vnp_Url"];
        string vnp_TmnCode = _configuration["VNPay:vnp_TmnCode"];
        string vnp_HashSecret = _configuration["VNPay:vnp_HashSecret"];

        VnPayLibrary vnpay = new VnPayLibrary();

        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
        vnpay.AddRequestData("vnp_Amount", ((int)(amount * 100)).ToString()); // Số tiền cần thanh toán
        vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", ipAddress);
        vnpay.AddRequestData("vnp_Locale", locale);
        vnpay.AddRequestData("vnp_OrderInfo", orderInfo);
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", orderId.ToString());

        if (!string.IsNullOrEmpty(bankCode))
        {
            vnpay.AddRequestData("vnp_BankCode", bankCode);
        }

        // Thời gian hết hạn thanh toán
        vnpay.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));

        // Tạo URL thanh toán
        string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

        return paymentUrl;
    }
}

public class VnPayLibrary
{
    private SortedList<string, string> requestData = new SortedList<string, string>();
    private SortedList<string, string> responseData = new SortedList<string, string>();

    public void AddRequestData(string key, string value)
    {
        requestData.Add(key, value);
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
        {
            responseData[key] = value; // Add or update key-value pairs in responseData
        }
    }

    public string GetResponseData(string key)
    {
        return responseData.ContainsKey(key) ? responseData[key] : null;
    }

    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        string queryString = string.Join("&", requestData.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        string signData = string.Join("&", requestData.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        // Tạo mã checksum (vnp_SecureHash)
        string vnpSecureHash = HmacSHA512(hashSecret, signData);
        queryString += $"&vnp_SecureHash={vnpSecureHash}";
        return $"{baseUrl}?{queryString}";
    }

    public bool ValidateSignature(string receivedSignature, string hashSecret)
    {
        // Lấy tất cả các tham số từ responseData trừ vnp_SecureHash
        var dataToSign = responseData
            .Where(kvp => kvp.Key != "vnp_SecureHash")
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}");

        // Tạo chuỗi dữ liệu cần ký
        string signData = string.Join("&", dataToSign);

        // Tạo chữ ký từ dữ liệu và khóa bí mật
        string calculatedSignature = HmacSHA512(hashSecret, signData);

        // So sánh chữ ký tính toán với chữ ký được nhận
        return string.Equals(calculatedSignature, receivedSignature, StringComparison.InvariantCultureIgnoreCase);
    }

    private string HmacSHA512(string key, string data)
    {
        var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return string.Concat(hashValue.Select(b => b.ToString("x2")));
    }
}

