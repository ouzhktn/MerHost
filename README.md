# MerHost - Fast Localhost Server

MerHost, Windows için geliştirilmiş, performans odaklı bir localhost sunucu çözümüdür. Nginx, PHP-FPM, MariaDB ve Node.js'yi tek bir uygulamada birleştirir.

## Özellikler

- ⚡ **Hızlı Kurulum** - Tek tıkla tüm sunucuları kur
- 🔒 **Otomatik SSL** - Her domain için otomatik sertifika oluşturma
- 🗄️ **phpMyAdmin** - Veritabanı yönetimi kolaylığı
- 🎯 **Virtual Hosts** - Manuel domain oluşturma
- ⚙️ **PHP Ayarları** - Hızlı PHP yapılandırma ayarları
- 🎨 **Modern UI** - Karanlık temalı modern arayüz
- 🔌 **Sistem Tray** - Arka planda çalışma desteği
- 🟢 **Node.js Desteği** - Local Node.js geliştirme
- 🔐 **Node.js + SSL** - Node.js projeleri için HTTPS desteği

## Ekran Görüntüleri

![Ana Ekran](Screenshot-G.png)

## Kurulum

1. `MerHost-Setup-1.1.0.exe` dosyasını indirin
2. Kurulumu çalıştırın
3. Uygulamayı başlatın

## Sistem Gereksinimleri

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (kurulum paketinde mevcut)

## Kullanım

### Sunucuyu Başlat
Tüm servisleri (Nginx, PHP, MySQL) tek tıkla başlatır

### Domain Oluştur
Yeni bir proje için domain oluşturun - Otomatik SSL ile

### Node.js Projeleri
- Proje Seç ile Node.js projenizi seçin
- npm install ile bağımlılıkları yükleyin
- npm start ile projeyi çalıştırın
- Domain oluştururken "Node.js Projesi" seçeneğini işaretleyin

## Varsayılan Portlar

| Servis | Port |
|--------|------|
| HTTP | 80 |
| HTTPS | 443 |
| MySQL | 3306 |
| PHP-FPM | 9000 |
| Node.js | 3000 |

## PHP Hızlı Ayarlar

- upload_max_filesize - Dosya yükleme limiti
- post_max_size - POST veri limiti
- max_execution_time - Max çalışma süresi
- memory_limit - Bellek limiti
- xdebug - Xdebug eklentisi

## www Klasörü Yapısı

```
www/
├── proje1/           # proje1.test
├── proje2/           # proje2.test  
├── node-projects/    # Node.js projeleri
│   └── myapp/       # myapp.test:3000
└── ...
```

## Lisans

MIT License

## Yazar

[Kingofa.com](https://kingofa.com)

---

**MerHost** - Kendi localhost sunucun, kendi kuralların!
