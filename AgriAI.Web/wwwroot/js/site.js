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

    // 2. Chatbot Language Configurations
    const chatLangDict = {
        en: {
            title: "AgriAI Farm Assistant",
            status: "Online (Gemini AI)",
            welcomeTitle: "Hello! 🙏 I am your AgriAI Assistant.",
            welcomeDesc: "Ask any question about crop diseases, fertilizers, organic farming, APMC Mandi rates, or marketplace help:",
            topics: ["🌱 Cotton Pest & Whitefly", "🥜 Groundnut White Grub", "🌿 Cumin Blight & Mildew", "🌾 Wheat Fertilizer & Yield", "🧪 Urea & Nano Urea Dose", "🧅 Onion & Garlic Bulb Sizing"],
            placeholder: "Type your question here...",
            offlineMsg: "⚠️ Trouble connecting to AgriAI. Please try again shortly.",
            emptyPrompt: "Please enter your question."
        },
        gu: {
            title: "AgriAI ખેતી સહાયક",
            status: "Online (Gemini AI)",
            welcomeTitle: "નમસ્તે! 🙏 હું તમારો AgriAI ખેતી આસિસ્ટન્ટ છું.",
            welcomeDesc: "પાકના રોગ, દવાઓનું ચોક્કસ માપ, જૈવિક ખાતર, મંડી બજાર ભાવ કે ખેતી સંબંધિત કોઈપણ પ્રશ્ન પૂછો:",
            topics: ["🌱 કપાસમાં ગુલાબી ઈયળ/સફેદ માખી", "🥜 મગફળીમાં સફેદ ધૈણ/સુકારો", "🌿 જીરુંમાં કાળી જીરી/ચરમી", "🌾 ઘઉંમાં ખાતર વ્યવસ્થાપન", "🧪 યૂરિયા/નેનો યૂરિયા ડોઝ", "🧅 ડુંગળી/લસણ સાઇઝ વધારવા"],
            placeholder: "તમારો પ્રશ્ન અહીં લખો...",
            offlineMsg: "⚠️ ખેતી આસિસ્ટન્ટ સાથે કનેક્ટ થવામાં મુશ્કેલી આવી છે. કૃપા કરીને થોડીવાર પછી પ્રયત્ન કરો.",
            emptyPrompt: "કૃપા કરીને તમારો પ્રશ્ન લખો."
        },
        hi: {
            title: "AgriAI कृषि सहायक",
            status: "Online (Gemini AI)",
            welcomeTitle: "नमस्ते! 🙏 मैं आपका AgriAI कृषि सहायक हूँ।",
            welcomeDesc: "फसल के रोग, कीटनाशक दवा की सही खुराक, जैविक खाद, मंडी भाव या खेती से जुड़े सवाल पूछें:",
            topics: ["🌱 कपास में कीट व सफेद मक्खी", "🥜 मूंगफली में सफेद लट", "🌿 जीरा में झुलसा व रोग", "🌾 गेहूं में खाद प्रबंधन", "🧪 यूरिया व नैनो यूरिया खुराक", "🧅 प्याज व लहसुन का आकार"],
            placeholder: "अपना सवाल यहाँ लिखें...",
            offlineMsg: "⚠️ कृषि सहायक से कनेक्ट होने में समस्या आई है। कृपया थोड़ी देर बाद प्रयास करें।",
            emptyPrompt: "कृपया अपना प्रश्न लिखें।"
        }
    };

    // Helper to get active language
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return '';
    }

    function getCurrentLanguage() {
        let savedLang = localStorage.getItem('agri_lang');
        if (savedLang) return savedLang;

        const selectEl = document.querySelector('.app-language-select');
        if (selectEl && selectEl.value) {
            return selectEl.value;
        }

        const cookieVal = getCookie('googtrans');
        if (cookieVal && cookieVal.includes('/gu')) return 'gu';
        if (cookieVal && cookieVal.includes('/hi')) return 'hi';
        return 'gu'; // Default to Gujarati for Gujarat farmers
    }

    let activeLang = getCurrentLanguage();

    // 3. Centralized Multi-Language Switcher (Synchronizes Header, Sidebar, and Chatbot in Real-Time)
    window.switchLanguageEverywhere = function (selectedLang, forceReload = false) {
        if (!selectedLang) return;
        localStorage.setItem('agri_lang', selectedLang);
        activeLang = selectedLang;

        // 1. Sync all language dropdowns on the page
        document.querySelectorAll('.app-language-select').forEach(sel => {
            if (sel.value !== selectedLang) {
                sel.value = selectedLang;
            }
        });

        // 2. Set Google Translate cookie
        const cookieValue = `/en/${selectedLang}`;
        document.cookie = `googtrans=${cookieValue}; path=/;`;
        if (window.location.hostname && window.location.hostname !== 'localhost') {
            document.cookie = `googtrans=${cookieValue}; path=/; domain=.${window.location.hostname};`;
        }

        // 3. Trigger Google Translate native combo live if loaded
        const gCombo = document.querySelector('.goog-te-combo');
        if (gCombo) {
            gCombo.value = selectedLang;
            gCombo.dispatchEvent(new Event('change'));
        }

        // 4. Update Chatbot UI instantly
        applyChatbotLanguage(selectedLang);

        if (forceReload) {
            const chatWidget = document.getElementById('agriChatWidget');
            if (chatWidget && !chatWidget.classList.contains('hidden')) {
                sessionStorage.setItem('agri_chat_open', 'true');
            }
            window.location.reload();
        }
    };

    // Attach listeners to all language select dropdowns
    const langSelects = document.querySelectorAll('.app-language-select');
    langSelects.forEach(select => {
        select.value = activeLang;
        select.addEventListener('change', function () {
            window.switchLanguageEverywhere(this.value, false);
        });
    });

    // Attach listeners to Chatbot Header Language Pills
    document.addEventListener('click', function (e) {
        const pill = e.target.closest('.chat-lang-pill');
        if (pill) {
            const lang = pill.getAttribute('data-lang');
            if (lang) {
                window.switchLanguageEverywhere(lang, false);
            }
        }
    });

    // 4. Dynamic Chatbot Language Setup
    function applyChatbotLanguage(lang) {
        const dict = chatLangDict[lang] || chatLangDict['gu'] || chatLangDict['en'];
        
        // Header Title
        const titleEl = document.querySelector('.agri-chat-title');
        if (titleEl) titleEl.textContent = dict.title;

        // Header Status Text
        const statusEl = document.getElementById('agriChatStatusText');
        if (statusEl) statusEl.textContent = dict.status;

        // Input Placeholder
        const inputEl = document.getElementById('agriChatInput');
        if (inputEl) inputEl.placeholder = dict.placeholder;

        // Header Lang Pills Active State
        document.querySelectorAll('.chat-lang-pill').forEach(pill => {
            if (pill.getAttribute('data-lang') === lang) {
                pill.classList.add('bg-white', 'text-dark', 'active');
                pill.classList.remove('text-white');
            } else {
                pill.classList.remove('bg-white', 'text-dark', 'active');
                pill.classList.add('text-white');
            }
        });

        // Welcome Message and Topic Pills in Chat Body
        const initialBotMsg = document.querySelector('#agriChatBody .agri-msg-bot:first-child .agri-msg-bubble') ||
                              document.querySelector('#agriChatBody .agri-msg-bot .agri-msg-bubble');
        if (initialBotMsg) {
            let pillsHtml = dict.topics.map(t => `<span class="topic-pill">${t}</span>`).join('');
            initialBotMsg.innerHTML = `
                <p class="mb-1"><strong>${dict.welcomeTitle}</strong></p>
                <p class="mb-2">${dict.welcomeDesc}</p>
                <div class="agri-quick-topics">
                    ${pillsHtml}
                </div>
            `;
        }
    }

    // Apply active language on initial load
    applyChatbotLanguage(activeLang);

    // 5. Smart Interactive Gemini AI Chatbot Widget
    const openChatBtn = document.getElementById('openChatBtn');
    const closeChatBtn = document.getElementById('closeChatBtn');
    const chatWidget = document.getElementById('agriChatWidget');
    const chatBody = document.getElementById('agriChatBody');
    const chatForm = document.getElementById('agriChatForm');
    const chatInput = document.getElementById('agriChatInput');

    // Auto-reopen chat if user was chatting before page reload
    if (sessionStorage.getItem('agri_chat_open') === 'true') {
        if (chatWidget) {
            chatWidget.classList.remove('hidden');
            applyChatbotLanguage(getCurrentLanguage());
        }
        sessionStorage.removeItem('agri_chat_open');
    }

    if (openChatBtn && chatWidget) {
        // Toggle Chat Window
        openChatBtn.addEventListener('click', function () {
            chatWidget.classList.toggle('hidden');
            if (!chatWidget.classList.contains('hidden')) {
                applyChatbotLanguage(getCurrentLanguage());
                chatInput.focus();
                scrollToBottom();
            }
        });

        if (closeChatBtn) {
            closeChatBtn.addEventListener('click', function () {
                chatWidget.classList.add('hidden');
            });
        }

        // Quick Topic Pills Click
        document.addEventListener('click', function (e) {
            if (e.target.classList.contains('topic-pill')) {
                const topicText = e.target.textContent.replace(/^[^\w\s\u0A80-\u0AFF\u0900-\u097F]+/g, '').trim();
                sendMessage(topicText);
            }
        });

        // Form Submit
        if (chatForm) {
            chatForm.addEventListener('submit', function (e) {
                e.preventDefault();
                const text = chatInput.value.trim();
                if (text) {
                    sendMessage(text);
                    chatInput.value = '';
                }
            });
        }
    }

    function scrollToBottom() {
        if (chatBody) {
            chatBody.scrollTop = chatBody.scrollHeight;
        }
    }

    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function(m) { return map[m]; });
    }

    function formatMarkdown(text) {
        if (!text) return '';
        let formatted = escapeHtml(text);
        // Bold: **text**
        formatted = formatted.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        // Italic: *text*
        formatted = formatted.replace(/\*(.*?)\*/g, '<em>$1</em>');
        // Bullet points
        formatted = formatted.replace(/^[\*\-]\s+(.*)$/gm, '<li class="ms-3">$1</li>');
        // Newlines
        formatted = formatted.replace(/\n/g, '<br/>');
        return formatted;
    }

    function sendMessage(messageText) {
        if (!messageText) return;
        const currentLang = getCurrentLanguage();
        const dict = chatLangDict[currentLang] || chatLangDict['en'];

        // Append User Message
        const userMsgDiv = document.createElement('div');
        userMsgDiv.className = 'agri-msg agri-msg-user';
        userMsgDiv.innerHTML = `<div class="agri-msg-bubble">${escapeHtml(messageText)}</div>`;
        chatBody.appendChild(userMsgDiv);
        scrollToBottom();

        // Append Typing Indicator
        const typingDiv = document.createElement('div');
        typingDiv.className = 'agri-msg agri-msg-bot typing-indicator-msg';
        typingDiv.innerHTML = `
            <div class="agri-msg-bubble">
                <div class="typing-dots">
                    <span class="typing-dot"></span>
                    <span class="typing-dot"></span>
                    <span class="typing-dot"></span>
                </div>
            </div>`;
        chatBody.appendChild(typingDiv);
        scrollToBottom();

        // Send to Gemini API backend with user's selected language
        fetch('/api/Chatbot/ask', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                message: messageText,
                language: currentLang
            })
        })
        .then(response => response.json())
        .then(data => {
            // Remove typing indicator
            typingDiv.remove();

            // Append Bot Response
            const botMsgDiv = document.createElement('div');
            botMsgDiv.className = 'agri-msg agri-msg-bot';
            const botText = data.response || data.Response || "Sorry, failed to get response.";
            botMsgDiv.innerHTML = `<div class="agri-msg-bubble">${formatMarkdown(botText)}</div>`;
            chatBody.appendChild(botMsgDiv);
            scrollToBottom();
        })
        .catch(error => {
            console.error('Chatbot error:', error);
            typingDiv.remove();

            const botMsgDiv = document.createElement('div');
            botMsgDiv.className = 'agri-msg agri-msg-bot';
            botMsgDiv.innerHTML = `<div class="agri-msg-bubble text-danger">${dict.offlineMsg}</div>`;
            chatBody.appendChild(botMsgDiv);
            scrollToBottom();
        });
    }

    // 6. Global Bulletproof Modal & Alert Close Handler (Guarantees every '✕' button works everywhere)
    document.addEventListener('click', function (e) {
        // A. Modal Close Button & Dismiss Triggers
        const dismissModalBtn = e.target.closest('[data-bs-dismiss="modal"]') || (e.target.classList.contains('btn-close') && e.target.closest('.modal'));
        if (dismissModalBtn) {
            e.preventDefault();
            e.stopPropagation();
            const modalEl = dismissModalBtn.closest('.modal');
            if (modalEl) {
                if (window.bootstrap && bootstrap.Modal) {
                    const modalInstance = bootstrap.Modal.getInstance(modalEl) || bootstrap.Modal.getOrCreateInstance(modalEl);
                    modalInstance.hide();
                } else {
                    modalEl.classList.remove('show');
                    modalEl.style.display = 'none';
                    document.body.classList.remove('modal-open');
                    document.querySelectorAll('.modal-backdrop').forEach(b => b.remove());
                }

                // Cleanup any backdrops that might get stuck
                setTimeout(function () {
                    if (!document.querySelector('.modal.show')) {
                        document.querySelectorAll('.modal-backdrop').forEach(b => b.remove());
                        document.body.classList.remove('modal-open');
                        document.body.style.removeProperty('overflow');
                        document.body.style.removeProperty('padding-right');
                    }
                }, 200);
            }
        }

        // B. Alert Close Button
        const dismissAlertBtn = e.target.closest('[data-bs-dismiss="alert"]') || (e.target.classList.contains('btn-close') && e.target.closest('.alert'));
        if (dismissAlertBtn) {
            e.preventDefault();
            e.stopPropagation();
            const alertEl = dismissAlertBtn.closest('.alert');
            if (alertEl) {
                if (window.bootstrap && bootstrap.Alert) {
                    const alertInstance = bootstrap.Alert.getInstance(alertEl) || bootstrap.Alert.getOrCreateInstance(alertEl);
                    alertInstance.close();
                } else {
                    alertEl.remove();
                }
            }
        }
    });
});
