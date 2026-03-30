# MerHost v1.1.0

MerHost - Windows için hızlı ve kolay localhost sunucu çözümü

## Özellikler

- ✅ **Nginx** - Web sunucusu
- ✅ **PHP-FPM** - PHP desteği
- ✅ **MariaDB** - Veritabanı
- ✅ **phpMyAdmin** - Veritabanı yönetimi
- ✅ **Node.js Desteği** - Local Node.js geliştirme
- ✅ **Otomatik SSL** - Ücretsiz SSL sertifikaları
- ✅ **Domain Yönetimi** - Kolay domain oluşturma
- ✅ **Node.js + SSL** - Node.js projeleri için HTTPS desteği

## Gereksinimler

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime

## Kurulum

1. `MerHost-Setup-1.1.0.exe` dosyasını çalıştır
2. Kurulum sihirbazını takip et
3. Uygulamayı başlat

## Kullanım

### Sunucuyu Başlatma
- "Sunucuyu Başlat" butonuna tıkla
- Tüm servisler (Nginx, PHP, MySQL) otomatik başlar

### PHP Projeleri
- `www` klasörüne projeni ekle
- Domain oluştur: "Domain Oluştur" butonu
- Otomatik SSL sertifikası ile HTTPS erişim

### Node.js Projeleri
- Proje Seç ile Node.js projeni seç
- npm install ile bağımlılıkları yükle
- npm start ile projeyi çalıştır
- Domain oluştururken "Node.js Projesi" seçeneğini işaretle
- Node.js portunu gir (varsayılan: 3000)
- Otomatik Nginx proxy ile HTTPS erişim

### phpMyAdmin
- http://localhost/phpmyadmin veya http://localhost:80/phpmyadmin
- Kullanıcı: root
- Şifre: (boş)

## Versiyon Geçmişi

### v1.1.0 (2026)
- Node.js desteği eklendi
- Node.js projeleri için SSL desteği eklendi
- Domain oluştururken Node.js seçeneği
- Nginx reverse proxy for Node.js
- Tarayıcıda aç butonu

### v1.0.0 (2026)
- İlk sürüm
- Nginx, PHP-FPM, MariaDB desteği
- Otomatik SSL sertifikaları
- Domain yönetimi

## Lisans

MIT License
