# Keten ERP - Production Deployment Kılavuzu
**Domain:** havalielaletleritamiri.com  
**Hosting:** Şirket PC'si (Windows)  
**SSL:** Caddy + Let's Encrypt (Otomatik)

---

## 1. YENİ CİHAZDA GEREKLİ YAZILIMLAR

### 1.1 Docker Desktop Kurulumu
1. https://www.docker.com/products/docker-desktop/ adresinden Windows için Docker Desktop'ı indirin
2. Kurulum sırasında "WSL 2" seçeneğini aktif edin
3. Kurulum sonrası Docker Desktop'ı başlatın
4. PowerShell'de test edin:
   ```powershell
   docker --version
   docker-compose --version
   ```

### 1.2 Git Kurulumu (Projeyi klonlamak için)
1. https://git-scm.com/download/win adresinden Git'i indirin ve kurun
2. PowerShell'de test edin:
   ```powershell
   git --version
   ```

---

## 2. PROJEYİ YENİ CİHAZA KOPYALAMA

### Seçenek A: GitHub üzerinden (Önerilen)
```powershell
# Projeyi klonla
cd C:\
git clone https://github.com/ugurkanyilmaz/web_based_erp_csharp-react.git keten_erp
cd keten_erp
```

### Seçenek B: Manuel kopyalama
- Proje klasörünü USB veya network üzerinden yeni cihaza kopyalayın
- Örnek konum: `C:\keten_erp\`

---

## 3. ENVIRONMENT VARIABLES AYARLAMA

### 3.1 `.env` dosyasını oluşturun
Proje kök dizininde `.env` dosyası oluşturun:

```powershell
cd C:\keten_erp
notepad .env
```

Aşağıdaki içeriği yapıştırın:

```env
# PostgreSQL
POSTGRES_USER=keten_admin_usr
POSTGRES_PASSWORD=Kt3nErP@2024!Scr3tDB#Pwd92xYz
POSTGRES_DB=ketenerp

# JWT Secret (256-bit random string)
JWT_SECRET_KEY=7mK9nP2qR5sT8vW1xZ4aC6bE0dF3gH7jL9mN2pQ5rS8tV1wX4yA6bC0dE3fG7hJ9k

# Domain ve Email
DOMAIN=havalielaletleritamiri.com
CADDY_EMAIL=admin@havalielaletleritamiri.com

# API Base URL (Production)
VITE_API_BASE_URL=https://havalielaletleritamiri.com
FRONTEND_URL=https://havalielaletleritamiri.com
```

**NOT:** Yukarıdaki değerler güvenli olarak oluşturulmuştur. İsterseniz değiştirebilirsiniz.

### 3.2 JWT Secret Key oluşturma (Opsiyonel)
Eğer farklı bir JWT key kullanmak isterseniz, PowerShell'de rastgele güçlü bir key üretin:

```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | ForEach-Object {[char]$_})
```

Çıkan sonucu kopyalayıp `.env` dosyasındaki `JWT_SECRET_KEY` değerine yapıştırın.

---

## 4. WINDOWS GÜVENLİK DUVARI AYARLARI

### 4.1 Firewall Port Açma
PowerShell'i **Administrator** olarak çalıştırın ve aşağıdaki komutları girin:

```powershell
# HTTP (80) - Let's Encrypt için gerekli
New-NetFirewallRule -DisplayName "HTTP (80) - Keten ERP" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 80

# HTTPS (443) - Production SSL
New-NetFirewallRule -DisplayName "HTTPS (443) - Keten ERP" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443
```

### 4.2 Kontrolü
Portların açık olduğunu kontrol edin:

```powershell
Get-NetFirewallRule -DisplayName "*Keten*" | Format-Table DisplayName, Enabled, Direction, Action
```

---

## 5. MODEM/ROUTER AYARLARI (PORT FORWARDING)

### 5.1 Cihazın Yerel IP Adresini Bulun
```powershell
ipconfig | findstr IPv4
```
Örnek: `192.168.1.100`

### 5.2 Router'da Port Yönlendirme
1. Router admin paneline giriş yapın (genellikle 192.168.1.1 veya 192.168.0.1)
2. **Port Forwarding** veya **NAT** bölümünü bulun
3. Şu kuralları ekleyin:

| Servis Adı | Dış Port | İç IP | İç Port | Protokol |
|------------|----------|--------|---------|----------|
| HTTP       | 80       | 192.168.1.100 | 80 | TCP |
| HTTPS      | 443      | 192.168.1.100 | 443 | TCP |

4. Ayarları kaydedin ve router'ı yeniden başlatın (gerekirse)

### 5.3 Public IP Adresinizi Öğrenin
```powershell
(Invoke-RestMethod -Uri 'https://api.ipify.org').trim()
```
veya https://whatismyipaddress.com/ adresini ziyaret edin.

**Örnek:** `88.247.125.45`

---

## 6. NATRO.COM DNS AYARLARI

### 6.1 Natro Panel'e Giriş
1. https://www.natro.com/ adresine gidin
2. Kullanıcı paneline giriş yapın
3. **Domain Yönetimi** → **havalielaletleritamiri.com** seçin

### 6.2 A Kaydı Ekleme
1. **DNS Yönetimi** veya **Nameserver Ayarları** bölümüne gidin
2. **A Kaydı Ekle**:
   - **Host:** @ (veya boş bırakın)
   - **Tip:** A
   - **IP Adresi:** `88.247.125.45` (Kendi public IP'nizi yazın)
   - **TTL:** 300 (5 dakika)
3. Kaydet

### 6.3 WWW CNAME Kaydı (Opsiyonel)
1. **CNAME Kaydı Ekle**:
   - **Host:** www
   - **Tip:** CNAME
   - **Değer:** havalielaletleritamiri.com
   - **TTL:** 300
2. Kaydet

### 6.4 DNS Propagasyonunu Kontrol Edin
PowerShell'de:
```powershell
Resolve-DnsName havalielaletleritamiri.com
```

Veya https://dnschecker.org/ adresinde global kontrolü yapın.

**Not:** DNS yayılması 5 dakika ile 48 saat arasında sürebilir (genelde 10-30 dakika).

---

## 7. DOCKER CONTAINER'LARI BAŞLATMA

### 7.1 Mevcut Container'ları Temizleme (Eğer varsa)
```powershell
cd C:\keten_erp
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml down -v
```

### 7.2 Production Build ve Start
```powershell
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build
```

### 7.3 Container Durumunu Kontrol
```powershell
docker ps
```

Şu container'lar çalışıyor olmalı:
- `ketenerp-postgres` (healthy)
- `ketenerp-api` (healthy)
- `ketenerp-frontend` (up)
- `ketenerp-caddy` (up)

### 7.4 Caddy Loglarını İzleyin
```powershell
docker logs -f ketenerp-caddy
```

**Beklenen çıktı:**
```
[INFO] obtaining certificate for havalielaletleritamiri.com
[INFO] certificate obtained successfully
[INFO] serving HTTPS on :443
```

Eğer hata görürseniz:
- **"DNS does not resolve"** → DNS ayarlarını kontrol edin (Adım 6)
- **"connection refused on port 80"** → Firewall ve port forwarding'i kontrol edin (Adım 4-5)
- **"rate limit exceeded"** → Let's Encrypt günlük limit aşımı, 1 saat bekleyin

---

## 8. TEST VE DOĞRULAMA

### 8.1 Local Test (Host üzerinden)
```powershell
# Health check
curl http://localhost/health

# API test
curl http://localhost:5000/api/health

# Frontend test (tarayıcıda)
start http://localhost
```

### 8.2 External Test (İnternet üzerinden)
1. **Farklı bir ağdan** (cep telefonu 4G/5G veya farklı internet) test edin:
   - https://havalielaletleritamiri.com

2. SSL Sertifikasını kontrol edin:
   - Tarayıcıda kilit ikonuna tıklayın
   - **Issued by:** Let's Encrypt
   - **Valid until:** (90 gün geçerli, Caddy otomatik yeniler)

3. SSL Labs ile test:
   - https://www.ssllabs.com/ssltest/analyze.html?d=havalielaletleritamiri.com

### 8.3 Login Test
1. https://havalielaletleritamiri.com adresini açın
2. Varsayılan kullanıcılarla giriş yapın:
   - **Admin:** `ugur` / `ugur762.`
   - **Muhasebe:** `muhasebe` / `keten@4145!`
   - **Teknik Servis:** `teknik` / `servis@1234`

---

## 9. ÜRETİM SONRASI YAPILMASI GEREKENLER

### 9.1 Varsayılan Şifreleri Değiştirin
1. Admin paneline giriş yapın
2. Tüm varsayılan kullanıcıların şifrelerini değiştirin
3. PostgreSQL şifresini de değiştirin (`.env` dosyasından)

### 9.2 Otomatik Yedekleme Ayarlayın
```powershell
# Yedekleme scriptini çalıştırın
cd C:\keten_erp
.\setup-backup.ps1
```

Bu script Windows Task Scheduler'da haftalık otomatik yedekleme oluşturur.

### 9.3 Windows Güncellemelerini Yönetin
- Otomatik yeniden başlatmaları kapatın veya bakım saatleri ayarlayın
- Docker Desktop'ın sistem başlangıcında otomatik başlamasını sağlayın

### 9.4 Monitoring ve Loglar
Container loglarını periyodik kontrol edin:
```powershell
# API logları
docker logs ketenerp-api --tail 100

# Caddy logları
docker logs ketenerp-caddy --tail 100

# PostgreSQL logları
docker logs ketenerp-postgres --tail 100
```

---

## 10. SORUN GİDERME

### SSL Sertifikası Alınamıyor
**Kontrol Listesi:**
1. DNS doğru mu? → `Resolve-DnsName havalielaletleritamiri.com`
2. Port 80 açık mı? → https://www.yougetsignal.com/tools/open-ports/
3. Firewall kuralları aktif mi? → `Get-NetFirewallRule -DisplayName "*Keten*"`
4. Router port forwarding doğru mu? → Router admin panelini kontrol et
5. Caddy loglarında ne yazıyor? → `docker logs ketenerp-caddy`

### Frontend Yüklenmiyor
1. Container çalışıyor mu? → `docker ps | findstr frontend`
2. Nginx logları: → `docker exec -it ketenerp-frontend cat /var/log/nginx/error.log`
3. API'ye bağlanabiliyor mu? → Tarayıcı developer console'da network sekmesini kontrol et

### API Hatası (500 Internal Server Error)
1. API loglarını kontrol et: `docker logs ketenerp-api --tail 50`
2. PostgreSQL bağlantısı çalışıyor mu? → `docker exec -it ketenerp-postgres psql -U ketenuser -d ketenerp -c "SELECT 1;"`
3. Environment variables doğru mu? → `.env` dosyasını kontrol et

### Database Bağlantı Hatası
```powershell
# PostgreSQL container içine gir
docker exec -it ketenerp-postgres psql -U ketenuser -d ketenerp

# Database var mı kontrol et
\l

# Tabloları listele
\dt
```

---

## 11. GÜVENLİK ÖNERİLERİ

### 11.1 Zorunlu
- ✅ `.env` dosyasını GitHub'a commit etmeyin (zaten `.gitignore`'da)
- ✅ Tüm varsayılan şifreleri değiştirin
- ✅ PostgreSQL portunu (5432) dışarıya açmayın
- ✅ Windows Update'i düzenli yapın

### 11.2 Önerilen
- 🔒 Windows Defender'ı aktif tutun
- 🔒 SSH/RDP yerine TeamViewer/AnyDesk kullanın (daha güvenli)
- 🔒 Fail2ban alternatifi kurun (deneme yanılma saldırıları için)
- 🔒 Caddy access loglarını düzenli kontrol edin

### 11.3 İleri Seviye
- 🛡️ Cloudflare üzerinden proxy kullanın (DDoS koruması)
- 🛡️ VPN ile yönetim erişimi sınırlandırın
- 🛡️ İki faktörlü kimlik doğrulama (2FA) ekleyin

---

## 12. DOCKER KOMUTLARI CHEAT SHEET

```powershell
# Container'ları başlat
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d

# Container'ları durdur
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml down

# Logları izle (tüm servisler)
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml logs -f

# Sadece API logları
docker logs -f ketenerp-api

# Container durumu
docker ps

# Container'a shell ile gir (troubleshooting)
docker exec -it ketenerp-api /bin/bash

# Yeniden build et (kod değişikliği sonrası)
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build

# Tüm container'ları ve volume'leri temizle (DİKKAT: VERİ SİLİNİR!)
docker-compose -f docker-compose.yml -f docker-compose.caddy.yml down -v

# Disk kullanımını kontrol et
docker system df

# Kullanılmayan image'leri temizle
docker image prune -a
```

---

## 13. DESTEK VE İLETİŞİM

Sorun yaşarsanız:
1. Önce bu kılavuzdaki **Sorun Giderme** bölümünü kontrol edin
2. Container loglarını toplayın: `docker-compose logs > logs.txt`
3. GitHub Issues'da yeni bir ticket açın

---

## ÖZET CHECKLIST

**Yeni cihazda kurulum için sıralı adımlar:**

- [ ] 1. Docker Desktop kur ve test et
- [ ] 2. Git kur (veya projeyi USB ile kopyala)
- [ ] 3. Projeyi `C:\keten_erp` klasörüne al
- [ ] 4. `.env` dosyasını oluştur ve şifreleri ayarla
- [ ] 5. Windows Firewall'da port 80 ve 443'ü aç (PowerShell Admin)
- [ ] 6. Router'da port forwarding ayarla (80, 443 → Host IP)
- [ ] 7. Public IP'ni öğren (`Invoke-RestMethod https://api.ipify.org`)
- [ ] 8. Natro.com'da DNS A kaydı oluştur (@ → Public IP)
- [ ] 9. DNS propagasyonunu bekle (5-30 dakika)
- [ ] 10. `docker-compose up -d --build` ile başlat
- [ ] 11. `docker logs -f ketenerp-caddy` ile sertifika alımını izle
- [ ] 12. https://havalielaletleritamiri.com adresini test et (dışarıdan)
- [ ] 13. Varsayılan şifreleri değiştir
- [ ] 14. Otomatik yedekleme ayarla (`.\setup-backup.ps1`)

---

**Hazırladı:** GitHub Copilot  
**Tarih:** 10 Kasım 2025  
**Versiyon:** 1.0
