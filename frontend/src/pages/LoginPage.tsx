import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { authApi } from "../services/api";

const emojis = ["💬", "💫", "🌈", "⭐", "🦋", "🌸", "🔥"];

const RESERVED = new Set(["admin", "administrator", "system", "moderator", "support", "root"]);

function isReserved(name: string): string | null {
    const lower = name.toLowerCase();
    if (RESERVED.has(lower)) return `"${name}" is a reserved username. Please choose another.`;
    if (lower.includes("admin")) return `Usernames containing "admin" are not allowed.`;
    return null;
}

const features = [
    {
        icon: "💬",
        title: "Free Chat Rooms",
        desc: "Join public or private chat rooms instantly. Connect with people from across Sri Lanka in real-time.",
    },
    {
        icon: "🕵️",
        title: "Anonymous Chat",
        desc: "Start chatting without revealing your identity. Pick a username and go. No email or phone required.",
    },
    {
        icon: "💰",
        title: "100% Free",
        desc: "No hidden charges, no premium plans. All features are completely free for Sri Lankan users.",
    },
    {
        icon: "🇱🇰",
        title: "Made for Sri Lanka",
        desc: "Designed for Sri Lankan users. Chat in Sinhala, Tamil, or English. Connect with people near you.",
    },
    {
        icon: "🔒",
        title: "Private DMs",
        desc: "Create private one-on-one chat rooms. Only invited members can join and see messages.",
    },
    {
        icon: "📱",
        title: "Works on Mobile",
        desc: "Optimized for mobile browsers. Chat on the go without downloading any app.",
    },
];

const steps = [
    { num: "1", icon: "✏️", title: "Pick a Name", desc: "Enter any username to get started instantly. No sign-up needed." },
    { num: "2", icon: "🏠", title: "Join a Room", desc: "Enter the public living room or create a private chat with a friend." },
    { num: "3", icon: "💬", title: "Start Chatting", desc: "Send messages, share images, and connect in real-time." },
];

export default function LoginPage() {
    const [username, setUsername] = useState("");
    const [error, setError] = useState("");
    const [floatingEmoji, setFloatingEmoji] = useState<string[]>([]);
    const navigate = useNavigate();

    useEffect(() => {
        const interval = setInterval(() => {
            setFloatingEmoji(prev => [...prev, emojis[Math.floor(Math.random() * emojis.length)]]);
            setTimeout(() => {
                setFloatingEmoji(prev => prev.slice(1));
            }, 4000);
        }, 800);
        return () => clearInterval(interval);
    }, []);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!username.trim()) return;
        setError("");

        const reservedMsg = isReserved(username.trim());
        if (reservedMsg) {
            setError(reservedMsg);
            return;
        }

        try {
            const res = await authApi.login(username.trim());
            localStorage.setItem("token", res.token);
            localStorage.setItem("userId", res.userId.toString());
            localStorage.setItem("username", res.username);
            navigate("/chat");
        } catch (err: any) {
            if (err.message?.includes("already in use")) {
                setError(`"${username}" is already in use. Try a different name.`);
            } else {
                setError(err.message || "Something went wrong");
            }
        }
    };

    const avatarPreview = username.trim()
        ? username.trim()[0].toUpperCase()
        : "?";

    const avatarColor = username.trim()
        ? `hsl(${username.split("").reduce((a, c) => a + c.charCodeAt(0), 0) % 360}, 70%, 60%)`
        : "#4f8cff";

    return (
        <div className="landing-page">
            {/* ── HERO SECTION ── */}
            <section className="hero-section" id="home">
                <div className="floating-emojis">
                    {floatingEmoji.map((emoji, i) => (
                        <span
                            key={i}
                            className="float-emoji"
                            style={{
                                left: `${10 + Math.random() * 80}%`,
                                animationDuration: `${3 + Math.random() * 2}s`,
                            }}
                        >
                            {emoji}
                        </span>
                    ))}
                </div>

                <div className="hero-content">
                    <div className="hero-badge">🔥 Free Online Chat in Sri Lanka</div>
                    <h1 className="hero-title">
                        Kamare Chat — <span className="highlight">Free Chat Rooms</span> for Sri Lankans
                    </h1>
                    <p className="hero-subtitle">
                        The best free online chat app in Sri Lanka. Join chat rooms, make new friends, 
                        and talk anonymously in Sinhala, Tamil, or English. No sign-up needed.
                    </p>

                    {/* Login Form (same as before, now in hero) */}
                    <div className="hero-card">
                        <div className="avatar-ring" style={{ "--avatar-color": avatarColor } as React.CSSProperties}>
                            <div className="avatar-preview" style={{ background: avatarColor }}>
                                {avatarPreview}
                            </div>
                        </div>
                        <h2>Start Chatting Now</h2>
                        <p className="hero-card-subtitle">Pick your username to begin</p>

                        <form onSubmit={handleSubmit}>
                            <div className="input-wrapper">
                                <span className="input-icon">@</span>
                                <input
                                    type="text"
                                    placeholder="Enter your username"
                                    value={username}
                                    onChange={(e) => setUsername(e.target.value)}
                                    maxLength={50}
                                    autoFocus
                                    required
                                />
                            </div>
                            {error && <p className="error">{error}</p>}
                            <button type="submit" className="join-btn">
                                <span>Start Chat</span>
                                <span className="btn-arrow">→</span>
                            </button>
                        </form>

                        <p className="hero-hint">🔒 Don't share personal information with strangers</p>
                    </div>
                </div>
            </section>

            {/* ── FEATURES SECTION ── */}
            <section className="features-section" id="features">
                <div className="section-container">
                    <h2 className="section-title">Why Kamare Chat?</h2>
                    <p className="section-subtitle">
                        The best free chat platform designed for Sri Lankan users
                    </p>

                    <div className="features-grid">
                        {features.map((f, i) => (
                            <div className="feature-card" key={i}>
                                <span className="feature-icon">{f.icon}</span>
                                <h3>{f.title}</h3>
                                <p>{f.desc}</p>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* ── HOW IT WORKS ── */}
            <section className="how-section" id="how-it-works">
                <div className="section-container">
                    <h2 className="section-title">How It Works</h2>
                    <p className="section-subtitle">Start chatting in 3 simple steps</p>

                    <div className="steps-row">
                        {steps.map((s, i) => (
                            <div className="step-card" key={i}>
                                <div className="step-number">{s.num}</div>
                                <span className="step-icon">{s.icon}</span>
                                <h3>{s.title}</h3>
                                <p>{s.desc}</p>
                                {i < steps.length - 1 && <div className="step-connector">→</div>}
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* ── SEO RICH TEXT / FAQ ── */}
            <section className="seo-section" id="about">
                <div className="section-container">
                    <h2 className="section-title">Free Online Chat Rooms in Sri Lanka</h2>

                    <div className="seo-content">
                        <div className="seo-block">
                            <h3>What is Kamare Chat?</h3>
                            <p>
                                Kamare Chat is a <strong>free online chat app</strong> designed specifically 
                                for users in <strong>Sri Lanka</strong>. Whether you want to chat anonymously, 
                                make new friends, or simply pass the time in a chat room, Kamare Chat provides 
                                a safe and welcoming environment. No email, phone number, or registration is 
                                required — just pick a name and start chatting immediately.
                            </p>
                        </div>

                        <div className="seo-block">
                            <h3>Why choose a Sri Lankan chat app?</h3>
                            <p>
                                Most global chat platforms don't cater to Sri Lankan users. Kamare Chat is 
                                <strong>built for Sri Lankans</strong>, with support for Sinhala and English 
                                conversations. Our servers are optimized for Sri Lankan internet connections, 
                                and the interface is designed keeping local users in mind. Whether you're in 
                                Colombo, Kandy, Galle, Jaffna, or anywhere else in Sri Lanka, you'll find 
                                people to chat with.
                            </p>
                        </div>

                        <div className="seo-block">
                            <h3>Is Kamare Chat really free?</h3>
                            <p>
                                Yes, 100% free. There are no premium tiers, no hidden charges, and no 
                                subscription fees. All features — including public chat rooms, private 
                                messages, and image sharing — are completely free for every user.
                            </p>
                        </div>

                        <div className="seo-block">
                            <h3>Can I chat anonymously?</h3>
                            <p>
                                Absolutely. Kamare Chat does not require any personal information. You 
                                don't need to provide an email address, phone number, or social media 
                                account. Simply choose a username and start chatting. Your privacy is 
                                protected — your chat history is not stored permanently, and you can 
                                leave any room at any time.
                            </p>
                        </div>

                        <div className="seo-block">
                            <h3>What languages are supported?</h3>
                            <p>
                                Kamare Chat supports <strong>Sinhala, Tamil, and English</strong>. 
                                You can chat in any language you're comfortable with. The interface is 
                                in English, but the chat rooms welcome conversations in all Sri Lankan 
                                languages. This makes it the ideal <strong>Sinhala chat room</strong> 
                                and <strong>English chat platform</strong> for Sri Lankans.
                            </p>
                        </div>

                        <div className="seo-block">
                            <h3>Best free chat rooms in Sri Lanka — alternatives</h3>
                            <p>
                                Looking for a <strong>chat app like Omegle but for Sri Lanka</strong>? 
                                Or a <strong>free alternative to Chatous or Y99</strong>? Kamare Chat 
                                fills the gap for Sri Lankan users who want a local, free, and anonymous 
                                chat experience. Unlike international platforms, Kamare Chat is 
                                moderated and designed with Sri Lankan cultural sensitivities in mind.
                                It's the <strong>best free online chat platform in Sri Lanka</strong> 
                                for meeting new people and having real conversations.
                            </p>
                        </div>
                    </div>
                </div>
            </section>

            {/* ── CTA BANNER ── */}
            <section className="cta-section">
                <div className="section-container">
                    <h2>Ready to start chatting?</h2>
                    <p>Join hundreds of Sri Lankans chatting right now. It's free and takes 10 seconds.</p>
                    <a href="#home" className="cta-button">Start Chat Now →</a>
                </div>
            </section>

            {/* ── FOOTER ── */}
            <footer className="landing-footer">
                <div className="section-container">
                    <div className="footer-grid">
                        <div className="footer-col">
                            <h4>Kamare Chat</h4>
                            <p>Free online chat rooms for Sri Lankans. Chat anonymously in Sinhala, Tamil, and English.</p>
                        </div>
                        <div className="footer-col">
                            <h4>Quick Links</h4>
                            <ul>
                                <li><a href="#home">Home</a></li>
                                <li><a href="#features">Features</a></li>
                                <li><a href="#how-it-works">How It Works</a></li>
                                <li><a href="#about">About</a></li>
                            </ul>
                        </div>
                        <div className="footer-col">
                            <h4>Disclaimer</h4>
                            <p className="footer-disclaimer">
                                By using Kamare Chat, you agree not to share personal, financial, or sensitive 
                                information. Be respectful to others. 
                                
                                Please note that site traffic will be used for analytical purposes.

                                This platform is for entertainment 
                                purposes only. We are not responsible for misuse by users.
                               
                                
                            </p>
                        </div>
                    </div>
                    <div className="footer-bottom">
                        <p>&copy; {new Date().getFullYear()} Kamare Chat. Free online chat app for Sri Lanka. 🇱🇰</p>
                    </div>
                </div>
            </footer>
        </div>
    );
}
