import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { authApi } from "../services/api";

const emojis = ["💬", "✨", "🚀", "🎉", "💫", "🌈", "⭐", "🦋", "🌸", "🔥"];

export default function LoginPage() {
    const [username, setUsername] = useState("");
    const [error, setError] = useState("");
    const [floatingEmoji, setFloatingEmoji] = useState<string[]>([]);
    const navigate = useNavigate();

    useEffect(() => {
        // Spawn floating emojis periodically
        const interval = setInterval(() => {
            setFloatingEmoji(prev => [...prev, emojis[Math.floor(Math.random() * emojis.length)]]);
            // Remove after animation
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
        <div className="login-page">
            {/* Floating emojis background */}
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

            <div className="login-card">
                {/* Animated badge */}
                <div className="login-badge">💬 Live Chat</div>

                {/* Avatar preview */}
                <div className="avatar-ring" style={{ "--avatar-color": avatarColor } as React.CSSProperties}>
                    <div className="avatar-preview" style={{ background: avatarColor }}>
                        {avatarPreview}
                    </div>
                </div>

                <h1>කාමරේ චැට්</h1>
                <p className="subtitle">ඔබ කැමති නමක් තෝරා ගන්න.</p>

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
                        <span>Enter Chat</span>
                        <span className="btn-arrow">→</span>
                    </button>
                </form>

                <p className="hint">
                    🔒සැමවිටම නිර්නාමිකව සිටින්න, විනෝද වන්න!
                </p>

                <div className="disclaimer">
                    <p className="disclaimer-title">📋 Disclaimer</p>
                    <p className="disclaimer-agree">By clicking "Enter Chat" you agree to the following conditions:</p>
                    <ul>
                        <li>පුද්ගලික, මූල්‍යමය හෝ සංවේදී තොරතුරු බෙදා නොගන්න</li>
                        <li>ගෞරවනීය වන්න , ඔබට සැලකීමට කැමති ආකාරයට අන් අයට සලකන්න.</li>
                        <li>පරිශීලකයින් විසින් කරන ලද කිසිදු අනිසි භාවිතයක් හෝ නොසැලකිලිමත් ක්‍රියාවක් සඳහා මෙම වෙබ් අඩවිය වගකිව යුතු නොවේ.</li>
                    </ul>
                </div>
            </div>
        </div>
    );
}
