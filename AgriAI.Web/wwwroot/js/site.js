// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", function () {
    // 1. Password Visibility Toggle
    document.addEventListener("click", function (e) {
        const eyeBtn = e.target.closest('.input-group-text') || e.target.closest('.eye-icon');
        if (eyeBtn) {
            const inputGroup = eyeBtn.closest('.input-group');
            if (inputGroup) {
                const passwordInput = inputGroup.querySelector('input');
                const icon = eyeBtn.querySelector('i');
                if (passwordInput && icon) {
                    if (passwordInput.type === "password") {
                        passwordInput.type = "text";
                        icon.classList.remove("bi-eye");
                        icon.classList.add("bi-eye-slash");
                    } else if (passwordInput.type === "text") {
                        passwordInput.type = "password";
                        icon.classList.remove("bi-eye-slash");
                        icon.classList.add("bi-eye");
                    }
                }
            }
        }
    });

    // 2. Multi-Language Switcher (English, Gujarati, Hindi)
    const langSelects = document.querySelectorAll('.app-language-select');
    
    // Read saved language or check googtrans cookie
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return '';
    }

    let savedLang = localStorage.getItem('agri_lang');
    if (!savedLang) {
        const cookieVal = getCookie('googtrans');
        if (cookieVal && cookieVal.includes('/gu')) savedLang = 'gu';
        else if (cookieVal && cookieVal.includes('/hi')) savedLang = 'hi';
        else savedLang = 'en';
    }

    langSelects.forEach(select => {
        select.value = savedLang;
        select.addEventListener('change', function () {
            const selectedLang = this.value;
            localStorage.setItem('agri_lang', selectedLang);

            // Set Google Translate cookie for domain and path
            const host = window.location.hostname;
            const cookieValue = `/en/${selectedLang}`;
            
            document.cookie = `googtrans=${cookieValue}; path=/; domain=${host}`;
            document.cookie = `googtrans=${cookieValue}; path=/;`;

            // Reload page to re-render in chosen language
            window.location.reload();
        });
    });

    // 3. Chatbot functionality
    const chatbotBtn = document.querySelector('.floating-chatbot');
    if (chatbotBtn) {
        chatbotBtn.addEventListener('click', function () {
            const userMessage = prompt("AgriAI Assistant (Type your question in Gujarati, Hindi or English):");
            if (userMessage) {
                fetch('/api/Chatbot/ask', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ message: userMessage })
                })
                .then(response => response.json())
                .then(data => {
                    alert("AgriAI: " + data.response);
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert("AgriAI is currently offline. Please try again later.");
                });
            }
        });
    }
});
