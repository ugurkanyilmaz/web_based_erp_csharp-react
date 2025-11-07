# E-mail Gönderimi Kurulum Rehberi

## Sorun
- "Teklif gönderildi" diyor ama e-mail gitmiyor
- "Connection refused" hatası alınıyor
- E-mail hesapları veritabanında görünmüyor

## Çözüm Adımları

### 1. Backend'i Çalıştırın
Backend sunucusu **mutlaka çalışıyor olmalı**:

```powershell
cd "C:\Users\uur\OneDrive\Masaüstü\keten_work\erp\KetenErp.Api"
dotnet run
```

Backend şu adreste çalışmalı: `http://localhost:5000`

Console'da şu mesajları göreceksiniz:
- `Ensured EmailAccounts table exists.`
- `Now listening on: http://localhost:5000`

### 2. E-mail Hesabı Ekleyin

1. Uygulamayı açın: `http://localhost:5173`
2. **Ayarlar > E-Posta Hesapları** sayfasına gidin: `http://localhost:5173/settings/email`
3. Yeni E-Posta Hesabı Ekle formunu doldurun:

**Gmail için Örnek Ayarlar:**
- **Görünen İsim**: `Firma Adı - Teklifler`
- **From adresi**: `sizin-email@gmail.com`
- **SMTP Host**: `smtp.gmail.com`
- **Port**: `587`
- **SMTP Kullanıcı Adı**: `sizin-email@gmail.com`
- **SMTP Parolası**: Gmail için **Uygulama Şifresi** (App Password) kullanmalısınız!
- **TLS/STARTTLS**: ✅ İşaretli

**Gmail Uygulama Şifresi Nasıl Alınır:**
1. Google hesabınızda 2 adımlı doğrulama açık olmalı
2. https://myaccount.google.com/apppasswords adresine gidin
3. "Uygulama şifreleri" oluşturun
4. 16 haneli şifreyi kopyalayın (boşluklar olmadan)

**Outlook/Hotmail için Örnek Ayarlar:**
- **SMTP Host**: `smtp-mail.outlook.com`
- **Port**: `587`
- **Kullanıcı Adı**: `sizin-email@outlook.com`
- **TLS**: ✅

**Yandex için Örnek Ayarlar:**
- **SMTP Host**: `smtp.yandex.com`
- **Port**: `587`
- **TLS**: ✅

### 3. Hesabı Aktif Yapın
- Eklediğiniz hesabın yanındaki **radio button**'a tıklayın
- "(Aktif)" etiketi görünmeli

### 4. Hesabı Test Edin
- Hesabın yanındaki **"Test"** butonuna tıklayın
- Başarılı olursa kendi e-mail adresinize test mesajı gidecek
- Hata varsa detaylı mesaj göreceksiniz

### 5. Teklif Gönderin
- Artık Muhasebe sayfasından "Teklif Gönder" butonuna tıklayabilirsiniz
- E-mail otomatik olarak müşteriye gönderilecek

## Sık Karşılaşılan Hatalar

### "Connection refused"
- **Sebep**: Backend sunucusu çalışmıyor
- **Çözüm**: Backend'i `dotnet run` ile başlatın

### "Authentication failed"
- **Sebep**: Yanlış kullanıcı adı veya şifre
- **Gmail için**: Normal şifre değil, Uygulama Şifresi kullanmalısınız
- **Çözüm**: Hesap bilgilerini kontrol edin, düzenleyin

### "SMTP Host not found"
- **Sebep**: SMTP host yanlış yazılmış
- **Çözüm**: Yukarıdaki doğru host adreslerini kullanın

### E-mail gönderildi ama gelmiyor
- Spam/çöp kutusunu kontrol edin
- E-mail sağlayıcınızın günlük limit'i olabilir
- Console'da detaylı log'ları kontrol edin

## Kod Değişiklikleri

### Yapılan İyileştirmeler:
1. ✅ localStorage fallback'leri kaldırıldı - artık sadece veritabanı kullanılıyor
2. ✅ Şifreler base64 ile encode ediliyor
3. ✅ Detaylı hata logları eklendi
4. ✅ Frontend'de hata mesajları düzgün gösteriliyor
5. ✅ Test butonu eklendi
6. ✅ E-mail gönderim durumu daha net bildiriliyor

### Backend Console Log'ları:
Backend çalışırken console'da şunları göreceksiniz:
```
[EmailService] Attempting to send email to: musteri@firma.com
[EmailService] Using account: Firma Adı - Teklifler (sizin@gmail.com)
[EmailService] SMTP Host: smtp.gmail.com:587, TLS: True
[EmailService] Password decoded from base64
[EmailService] Attached PDF: /path/to/pdf
[EmailService] Sending email...
[EmailService] Email sent successfully to musteri@firma.com
```

Hata durumunda:
```
[EmailService] ERROR: E-mail gönderme hatası: The SMTP server requires a secure connection...
```

## Test Senaryosu

1. Backend'i başlat: `dotnet run`
2. Frontend'i başlat: `npm run dev` 
3. Settings/Email'e git
4. Gmail hesabınızı ekle (uygulama şifresi ile)
5. Test butonuna tıkla
6. E-mail geldi mi kontrol et
7. Muhasebe'den teklif gönder
8. Müşteriye e-mail gitti mi kontrol et

## İletişim
Sorun devam ederse:
1. Backend console log'larını paylaşın
2. Browser console'u kontrol edin (F12)
3. E-mail sağlayıcınızın SMTP ayarlarını doğrulayın
