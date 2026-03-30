using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MerHost.Services;

public class DomainMapping
{
    public string Domain { get; set; } = "";
    public string Folder { get; set; } = "";
    public bool IsNode { get; set; } = false;
    public int NodePort { get; set; } = 3000;
}

public class VirtualHostManager
{
    private readonly BinaryManager _binaryManager;
    private readonly string _sslPath;
    private readonly string _domainsFile;
    private List<DomainMapping> _customDomains = new();

    public event Action<int, string>? OnLog;

    public VirtualHostManager(BinaryManager binaryManager)
    {
        _binaryManager = binaryManager;
        _sslPath = Path.Combine(_binaryManager.AppDataPath, "ssl");
        _domainsFile = Path.Combine(_binaryManager.AppDataPath, "domains.json");
        
        if (!Directory.Exists(_sslPath))
            Directory.CreateDirectory(_sslPath);
            
        LoadDomains();
    }

    private void LoadDomains()
    {
        try
        {
            if (File.Exists(_domainsFile))
            {
                var json = File.ReadAllText(_domainsFile);
                var domains = JsonSerializer.Deserialize<List<DomainMapping>>(json) ?? new();
                foreach (var d in domains)
                {
                    if (!d.IsNode)
                    {
                        d.IsNode = false;
                    }
                    if (d.NodePort == 0)
                    {
                        d.NodePort = 3000;
                    }
                }
                _customDomains = domains;
            }
        }
        catch
        {
            _customDomains = new();
        }
    }

    private void SaveDomains()
    {
        try
        {
            var json = JsonSerializer.Serialize(_customDomains, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_domainsFile, json);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Domain kaydetme hatası: {ex.Message}");
        }
    }

    public bool CreateDomain(string domainName, string folderName)
    {
        try
        {
            var fullDomain = $"{domainName}.test";
            
            if (_customDomains.Any(d => d.Domain == fullDomain))
            {
                OnLog?.Invoke(1, "Bu domain zaten mevcut!");
                return false;
            }
            
            var folderPath = Path.Combine(_binaryManager.WwwPath, folderName);
            if (!Directory.Exists(folderPath))
            {
                OnLog?.Invoke(1, $"Klasör bulunamadı: {folderName}");
                return false;
            }
            
            _customDomains.Add(new DomainMapping { Domain = fullDomain, Folder = folderName });
            SaveDomains();
            
            OnLog?.Invoke(0, $"Domain oluşturuluyor: {fullDomain} -> {folderName}");
            
            UpdateHostsFile();
            GenerateSslCerts(new List<DomainMapping> { new() { Domain = fullDomain, Folder = folderName } });
            UpdateNginxConfig();
            
            OnLog?.Invoke(0, $"Domain hazır: https://{fullDomain}");
            return true;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Domain oluşturma hatası: {ex.Message}");
            return false;
        }
    }

    public bool CreateNodeDomain(string domainName, string folderName, int nodePort = 3000)
    {
        try
        {
            var fullDomain = $"{domainName}.test";
            
            if (_customDomains.Any(d => d.Domain == fullDomain))
            {
                OnLog?.Invoke(1, "Bu domain zaten mevcut!");
                return false;
            }
            
            var folderPath = Path.Combine(_binaryManager.WwwPath, folderName);
            if (!Directory.Exists(folderPath))
            {
                OnLog?.Invoke(1, $"Klasör bulunamadı: {folderName}");
                return false;
            }
            
            _customDomains.Add(new DomainMapping 
            { 
                Domain = fullDomain, 
                Folder = folderName,
                IsNode = true,
                NodePort = nodePort
            });
            SaveDomains();
            
            OnLog?.Invoke(0, $"Node.js Domain oluşturuluyor: {fullDomain} -> localhost:{nodePort}");
            
            UpdateHostsFile();
            GenerateSslCerts(new List<DomainMapping> { new() { Domain = fullDomain, Folder = folderName, IsNode = true, NodePort = nodePort } });
            UpdateNginxConfig();
            
            OnLog?.Invoke(0, $"Node.js Domain hazır: https://{fullDomain}");
            return true;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Domain oluşturma hatası: {ex.Message}");
            return false;
        }
    }

    public List<string> GetAllDomains()
    {
        var domains = new List<string>();
        foreach (var dm in _customDomains)
        {
            domains.Add(dm.Domain);
        }
        return domains;
    }

    public Dictionary<string, string> GetDomainFolderMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var dm in _customDomains)
        {
            map[dm.Domain] = dm.Folder;
        }
        return map;
    }

    public void UpdateVirtualHosts()
    {
        try
        {
            LoadDomains();
            OnLog?.Invoke(0, $"www path: {_binaryManager.WwwPath}");
            
            if (_customDomains.Count == 0)
            {
                OnLog?.Invoke(0, "Domain bulunamadı! 'Domain Oluştur' butonu ile ekleyin.");
            }
            
            UpdateHostsFile();
            GenerateSslCerts(_customDomains);
            UpdateNginxConfig();
            
            OnLog?.Invoke(0, $"{_customDomains.Count} virtual host yapılandırıldı");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Virtual host hatası: {ex.Message}");
        }
    }

    public List<string> GetProjects()
    {
        var projects = new List<string>();
        var wwwPath = _binaryManager.WwwPath;

        OnLog?.Invoke(0, $"GetProjects - wwwPath: {wwwPath}");
        
        if (!Directory.Exists(wwwPath))
        {
            OnLog?.Invoke(1, $"www klasörü yok: {wwwPath}");
            return projects;
        }

        var dirs = Directory.GetDirectories(wwwPath);
        OnLog?.Invoke(0, $"Klasörler: {string.Join(", ", dirs)}");
        
        foreach (var dir in dirs)
        {
            var dirName = Path.GetFileName(dir);
            if (dirName != "phpmyadmin" && !dirName.StartsWith("."))
            {
                projects.Add(dirName);
            }
        }
        return projects;
    }

    private void UpdateHostsFile()
    {
        var hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
        
        var entries = new List<string>
        {
            "127.0.0.1 localhost",
            "127.0.0.1 merhost.local"
        };
        
        foreach (var dm in _customDomains)
        {
            entries.Add($"127.0.0.1 {dm.Domain}");
        }
        
        if (AdminHelper.IsRunningAsAdmin())
        {
            var allLines = new List<string>();
            if (File.Exists(hostsPath))
            {
                var existingLines = File.ReadAllLines(hostsPath);
                foreach (var line in existingLines)
                {
                    if (!line.Contains(".test") && !line.Contains("merhost.local") && !line.Contains("merhost"))
                    {
                        allLines.Add(line);
                    }
                }
            }
            allLines.AddRange(entries);
            
            try
            {
                File.WriteAllLines(hostsPath, allLines);
                OnLog?.Invoke(0, "Hosts dosyası güncellendi");
            }
            catch (UnauthorizedAccessException)
            {
                AdminHelper.UpdateHostsFileWithElevation(entries, msg => OnLog?.Invoke(0, msg));
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(1, $"Hosts hatası: {ex.Message}");
            }
        }
        else
        {
            AdminHelper.UpdateHostsFileWithElevation(entries, msg => OnLog?.Invoke(0, msg));
        }
    }

    private void GenerateSslCerts(List<DomainMapping> domains)
    {
        foreach (var dm in domains)
        {
            var domainName = dm.Domain.Replace(".test", "");
            var certPath = Path.Combine(_sslPath, $"{domainName}.crt");
            var keyPath = Path.Combine(_sslPath, $"{domainName}.key");
            var pfxPath = Path.Combine(_sslPath, $"{domainName}.pfx");

            if (!File.Exists(pfxPath))
            {
                try
                {
                    CreateSelfSignedCert(domainName, certPath, keyPath, pfxPath);
                    InstallCertToTrustedRoot(certPath);
                    OnLog?.Invoke(0, $"SSL sertifikası oluşturuldu ve yüklendi: {dm.Domain}");
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke(1, $"Sertifika hatası ({dm.Domain}): {ex.Message}");
                }
            }
        }

        var localhostCert = Path.Combine(_sslPath, "localhost.crt");
        if (!File.Exists(localhostCert))
        {
            try
            {
                CreateSelfSignedCert("localhost", localhostCert, 
                    Path.Combine(_sslPath, "localhost.key"), 
                    Path.Combine(_sslPath, "localhost.pfx"));
                InstallCertToTrustedRoot(localhostCert);
                OnLog?.Invoke(0, "localhost SSL sertifikası oluşturuldu");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(1, $"localhost sertifika hatası: {ex.Message}");
            }
        }
    }

    private void InstallCertToTrustedRoot(string certPath)
    {
        try
        {
            var cert = new X509Certificate2(certPath);
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            
            var existingCerts = store.Certificates.Find(X509FindType.FindBySubjectName, cert.Subject, false);
            if (existingCerts.Count == 0)
            {
                store.Add(cert);
                OnLog?.Invoke(0, "Sertifika güvenilir köke eklendi");
            }
            store.Close();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Sertifika yükleme hatası: {ex.Message}");
        }
    }

    private void CreateSelfSignedCert(string projectName, string certPath, string keyPath, string pfxPath)
    {
        var domain = $"{projectName}.test";
        
        using var rsa = RSA.Create(2048);
        
        var request = new CertificateRequest(
            $"CN={domain}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        sanBuilder.AddDnsName($"*.{domain}");
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(10));
            
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            
        var pfxBytes = certificate.Export(X509ContentType.Pfx, "");
        File.WriteAllBytes(pfxPath, pfxBytes);

        File.WriteAllText(certPath, ExportToPem(certificate, "CERTIFICATE"));
        File.WriteAllText(keyPath, ExportToPem(rsa, "PRIVATE KEY"));
    }

    private string ExportToPem(X509Certificate2 cert, string label)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"-----BEGIN {label}-----");
        var base64 = Convert.ToBase64String(cert.RawData);
        for (int i = 0; i < base64.Length; i += 64)
        {
            builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        builder.AppendLine($"-----END {label}-----");
        return builder.ToString();
    }

    private string ExportToPem(RSA rsa, string label)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"-----BEGIN {label}-----");
        var base64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        for (int i = 0; i < base64.Length; i += 64)
        {
            builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        builder.AppendLine($"-----END {label}-----");
        return builder.ToString();
    }

    private void UpdateNginxConfig()
    {
        var confDir = Path.Combine(_binaryManager.BinPath, "nginx", "conf");
        var nginxConf = Path.Combine(confDir, "nginx.conf");
        
        OnLog?.Invoke(0, $"Nginx config yazılıyor: {nginxConf}");

        var config = new StringBuilder();
        config.AppendLine("worker_processes 1;");
        config.AppendLine("error_log logs/error.log;");
        config.AppendLine("pid logs/nginx.pid;");
        config.AppendLine();
        config.AppendLine("events {");
        config.AppendLine("    worker_connections 1024;");
        config.AppendLine("}");
        config.AppendLine();
        config.AppendLine("http {");
        config.AppendLine("    include mime.types;");
        config.AppendLine("    default_type application/octet-stream;");
        config.AppendLine("    sendfile on;");
        config.AppendLine("    keepalive_timeout 65;");
        config.AppendLine();
        
        config.AppendLine("    # HTTP Server");
        config.AppendLine("    server {");
        config.AppendLine("        listen 80;");
        config.AppendLine("        server_name localhost merhost.local;");
        config.AppendLine($"        root \"{_binaryManager.WwwPath.Replace("\\", "/")}\";");
        config.AppendLine("        index index.php index.html index.htm;");
        config.AppendLine();
        config.AppendLine("        location / {");
        config.AppendLine("            try_files $uri $uri/ /index.php?$query_string;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location /phpmyadmin {");
        config.AppendLine($"            alias \"{_binaryManager.PhpMyAdminPath.Replace("\\", "/")}/\";");
        config.AppendLine("            index index.php;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location ~ ^/phpmyadmin/(.+\\.php)$ {");
        config.AppendLine($"            alias \"{_binaryManager.PhpMyAdminPath.Replace("\\", "/")}/$1\";");
        config.AppendLine("            fastcgi_pass 127.0.0.1:9000;");
        config.AppendLine("            fastcgi_index index.php;");
        config.AppendLine("            fastcgi_param SCRIPT_FILENAME $request_filename;");
        config.AppendLine("            include fastcgi_params;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location ~ \\.php$ {");
        config.AppendLine("            fastcgi_pass 127.0.0.1:9000;");
        config.AppendLine("            fastcgi_index index.php;");
        config.AppendLine("            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        config.AppendLine("            include fastcgi_params;");
        config.AppendLine("        }");
        config.AppendLine("    }");
        config.AppendLine();

        config.AppendLine("    # HTTPS Server - localhost");
        config.AppendLine("    server {");
        config.AppendLine("        listen 443 ssl;");
        config.AppendLine("        server_name localhost merhost.local;");
        config.AppendLine($"        root \"{_binaryManager.WwwPath.Replace("\\", "/")}\";");
        config.AppendLine("        index index.php index.html index.htm;");
        config.AppendLine();
        config.AppendLine($"        ssl_certificate \"{_sslPath.Replace("\\", "/")}/localhost.crt\";");
        config.AppendLine($"        ssl_certificate_key \"{_sslPath.Replace("\\", "/")}/localhost.key\";");
        config.AppendLine("        ssl_protocols TLSv1.2 TLSv1.3;");
        config.AppendLine("        ssl_ciphers HIGH:!aNULL:!MD5;");
        config.AppendLine("        ssl_prefer_server_ciphers on;");
        config.AppendLine();
        config.AppendLine("        location / {");
        config.AppendLine("            try_files $uri $uri/ /index.php?$query_string;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location /phpmyadmin {");
        config.AppendLine($"            alias \"{_binaryManager.PhpMyAdminPath.Replace("\\", "/")}/\";");
        config.AppendLine("            index index.php;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location ~ ^/phpmyadmin/(.+\\.php)$ {");
        config.AppendLine($"            alias \"{_binaryManager.PhpMyAdminPath.Replace("\\", "/")}/$1\";");
        config.AppendLine("            fastcgi_pass 127.0.0.1:9000;");
        config.AppendLine("            fastcgi_index index.php;");
        config.AppendLine("            fastcgi_param SCRIPT_FILENAME $request_filename;");
        config.AppendLine("            include fastcgi_params;");
        config.AppendLine("        }");
        config.AppendLine();
        config.AppendLine("        location ~ \\.php$ {");
        config.AppendLine("            fastcgi_pass 127.0.0.1:9000;");
        config.AppendLine("            fastcgi_index index.php;");
        config.AppendLine("            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        config.AppendLine("            include fastcgi_params;");
        config.AppendLine("        }");
        config.AppendLine("    }");
        config.AppendLine();

        foreach (var dm in _customDomains)
        {
            var domainName = dm.Domain.Replace(".test", "");
            var projectPath = Path.Combine(_binaryManager.WwwPath, dm.Folder).Replace("\\", "/");
            var certPath = Path.Combine(_sslPath, $"{domainName}.crt").Replace("\\", "/");
            var keyPath = Path.Combine(_sslPath, $"{domainName}.key").Replace("\\", "/");

            config.AppendLine($"    # {dm.Domain} ({(dm.IsNode ? "Node.js" : "PHP")})");
            config.AppendLine("    server {");
            config.AppendLine($"        listen 80;");
            config.AppendLine($"        server_name {dm.Domain};");
            config.AppendLine($"        return 301 https://$server_name$request_uri;");
            config.AppendLine("    }");
            config.AppendLine();
            config.AppendLine("    server {");
            config.AppendLine($"        listen 443 ssl;");
            config.AppendLine($"        server_name {dm.Domain};");
            
            if (dm.IsNode)
            {
                config.AppendLine("        location / {");
                config.AppendLine($"            proxy_pass http://127.0.0.1:{dm.NodePort};");
                config.AppendLine("            proxy_http_version 1.1;");
                config.AppendLine("            proxy_set_header Upgrade $http_upgrade;");
                config.AppendLine("            proxy_set_header Connection 'upgrade';");
                config.AppendLine("            proxy_set_header Host $host;");
                config.AppendLine("            proxy_cache_bypass $http_upgrade;");
                config.AppendLine("        }");
            }
            else
            {
                config.AppendLine($"        root \"{projectPath}\";");
                config.AppendLine("        index index.php index.html index.htm;");
                config.AppendLine();
                config.AppendLine("        location / {");
                config.AppendLine("            try_files $uri $uri/ /index.php?$query_string;");
                config.AppendLine("        }");
                config.AppendLine();
                config.AppendLine("        location ~ \\.php$ {");
                config.AppendLine("            fastcgi_pass 127.0.0.1:9000;");
                config.AppendLine("            fastcgi_index index.php;");
                config.AppendLine("            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
                config.AppendLine("            include fastcgi_params;");
                config.AppendLine("        }");
            }
            
            config.AppendLine();
            config.AppendLine($"        ssl_certificate {certPath};");
            config.AppendLine($"        ssl_certificate_key {keyPath};");
            config.AppendLine("        ssl_protocols TLSv1.2 TLSv1.3;");
            config.AppendLine("        ssl_ciphers HIGH:!aNULL:!MD5;");
            config.AppendLine("        ssl_prefer_server_ciphers on;");
            config.AppendLine("    }");
            config.AppendLine();
        }

        config.AppendLine("}");

        File.WriteAllText(nginxConf, config.ToString());
    }

    public List<string> GetProjectDomains()
    {
        LoadDomains();
        return GetAllDomains();
    }
}
