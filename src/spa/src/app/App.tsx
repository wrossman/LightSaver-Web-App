import { BrowserRouter, Routes, Route } from "react-router-dom";
import HomePage from "./pages/HomePage";
import LinkPage from "./pages/LinkPage";
import UploadSuccess from "../features/upload-status/components/UploadSuccess";

function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<HomePage />} />
                <Route path="/link" element={<LinkPage />} />
                <Route path="/UploadSuccess" element={<UploadSuccess />} />
                <Route path="*" element={<HomePage />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;