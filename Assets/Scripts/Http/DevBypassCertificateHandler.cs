using UnityEngine.Networking;

/// <summary>
/// 开发环境用：跳过 TLS 证书校验（自签名、IP 访问、CN 不匹配等）。
/// 仅应在内网调试时通过配置开启，生产环境务必关闭。
/// </summary>
public sealed class DevBypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}
